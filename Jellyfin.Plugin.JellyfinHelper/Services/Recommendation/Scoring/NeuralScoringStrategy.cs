using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Scoring;

/// <summary>
///     Neural network scoring strategy using a two-hidden-layer MLP (Multi-Layer Perceptron).
///     Learns non-linear feature interactions from user watch history via backpropagation.
///     Architecture: 23 inputs → 16 hidden₁ (ReLU) → 8 hidden₂ (ReLU) → 1 output (Sigmoid) = 529 parameters.
///     Optimized for NAS/Docker with limited hardware: zero-allocation scoring path,
///     pre-allocated training buffers, ~530 FP multiplications per score.
///     No external ML dependencies — pure C# implementation.
/// </summary>
/// <remarks>
///     Training uses Adam optimizer with L2 regularization, Z-score feature standardization,
///     Xavier weight initialization, temporal sample weighting, and early stopping.
///     Genre-mismatch penalties are NOT applied here — handled centrally by the ensemble layer.
///     Weights are persisted to disk so they survive server restarts.
/// </remarks>
public sealed class NeuralScoringStrategy : IScoringStrategy, ITrainableStrategy, IDisposable
{
    /// <summary>Number of neurons in the first hidden layer.</summary>
    internal const int Hidden1Size = 16;

    /// <summary>Number of neurons in the second hidden layer.</summary>
    internal const int Hidden2Size = 8;

    /// <summary>Default learning rate for Adam optimizer.</summary>
    internal const double DefaultLearningRate = 0.005;

    /// <summary>L2 regularization strength (weight decay).</summary>
    internal const double L2Lambda = 0.001;

    /// <summary>Adam β1 (first moment exponential decay rate).</summary>
    internal const double AdamBeta1 = 0.9;

    /// <summary>Adam β2 (second moment exponential decay rate).</summary>
    internal const double AdamBeta2 = 0.999;

    /// <summary>Adam ε for numerical stability.</summary>
    internal const double AdamEpsilon = 1e-8;

    /// <summary>Maximum training epochs per <see cref="Train"/> call.</summary>
    internal const int MaxTrainingEpochs = 50;

    /// <summary>Minimum training examples required before training runs.</summary>
    internal const int MinTrainingExamples = 8;

    /// <summary>Consecutive epochs without improvement before early stopping triggers.</summary>
    internal const int EarlyStoppingPatience = 5;

    /// <summary>Fraction of examples used for validation.</summary>
    internal const double ValidationSplitRatio = 0.2;

    /// <summary>Minimum validation examples required for early stopping.</summary>
    internal const int MinValidationExamples = 2;

    /// <summary>Minimum examples before Z-score standardization is applied.</summary>
    internal const int MinExamplesForStandardization = 10;

    /// <summary>Weight clamp magnitude to prevent gradient explosion.</summary>
    internal const double WeightClamp = 3.0;

    /// <summary>Minimum sample weight below which a training example is skipped (temporal decay floor).</summary>
    internal const double MinSampleWeight = 0.01;

    /// <summary>Early stopping improvement threshold (avoids triggering on noise).</summary>
    internal const double EarlyStoppingMinDelta = 1e-6;

    /// <summary>Maximum epochs when early stopping is disabled (fewer epochs to avoid overfitting).</summary>
    internal const int MaxEpochsWithoutEarlyStopping = 20;

    /// <summary>Schema version for persisted weights. Increment on architecture changes.</summary>
    internal const int CurrentWeightsVersion = 3;

    /// <summary>Legacy constant kept for backward compatibility with tests. Maps to <see cref="Hidden2Size"/>.</summary>
    internal const int HiddenSize = Hidden2Size;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    // Readonly fields first (SA1214)
    private readonly ILogger? _logger;
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly object _syncRoot = new();
    private readonly string? _weightsPath;

    /// <summary>Thread-local scratch buffers to avoid contention on the hot Score() path.</summary>
    [ThreadStatic]
    private static double[]? _tlsH1Pre;
    [ThreadStatic]
    private static double[]? _tlsH1Act;
    [ThreadStatic]
    private static double[]? _tlsH2Pre;
    [ThreadStatic]
    private static double[]? _tlsH2Act;

    // Non-readonly fields — Adam moment arrays for input→hidden1
    private int _adamTimestep;
    private double[] _biasH1;
    private double[] _biasH2;
    private double _biasOutput;
    private volatile bool _disposed;
    private double[]? _featureMeans;
    private double[]? _featureStdDevs;
    private double _lastValidationLoss = double.NaN;
    private double[]? _mBH1;
    private double[]? _mBH2;
    private double _mBO;
    private double[]? _mWH1H2;
    private double[]? _mWH2O;
    private double[]? _mWIH;
    private int _trainingGeneration;
    private double[]? _vBH1;
    private double[]? _vBH2;
    private double _vBO;
    private double[]? _vWH1H2;
    private double[]? _vWH2O;
    private double[]? _vWIH;
    private double[] _weightsH1H2;
    private double[] _weightsH2O;
    private double[] _weightsIH;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NeuralScoringStrategy"/> class
    ///     with Xavier-initialized weights for stable gradient flow.
    /// </summary>
    /// <param name="weightsPath">Optional file path for persisting learned weights.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public NeuralScoringStrategy(string? weightsPath = null, ILogger? logger = null)
    {
        _weightsPath = weightsPath;
        _logger = logger;

        var inputSize = CandidateFeatures.FeatureCount;
        _weightsIH = new double[Hidden1Size * inputSize];
        _biasH1 = new double[Hidden1Size];
        _weightsH1H2 = new double[Hidden2Size * Hidden1Size];
        _biasH2 = new double[Hidden2Size];
        _weightsH2O = new double[Hidden2Size];
        _biasOutput = 0.0;

        InitializeXavier(inputSize);
        TryLoadWeights();
    }

    /// <inheritdoc />
    public string Name => "Neural (Adaptive MLP)";

    /// <inheritdoc />
    public string NameKey => "strategyNeural";

    /// <summary>
    ///     Gets the validation loss from the last training run.
    ///     Used by <see cref="EnsembleScoringStrategy"/> to compare against the linear model.
    /// </summary>
    internal double LastValidationLoss
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastValidationLoss;
            }
        }
    }

    /// <summary>Gets a copy of the input→hidden1 layer weights (for testing).</summary>
    internal double[] CurrentWeightsHidden
    {
        get
        {
            lock (_syncRoot)
            {
                return (double[])_weightsIH.Clone();
            }
        }
    }

    /// <summary>Gets a copy of the hidden2→output layer weights (for testing).</summary>
    internal double[] CurrentWeightsOutput
    {
        get
        {
            lock (_syncRoot)
            {
                return (double[])_weightsH2O.Clone();
            }
        }
    }

    /// <summary>Gets a copy of the hidden1→hidden2 layer weights (for testing).</summary>
    internal double[] CurrentWeightsH1H2
    {
        get
        {
            lock (_syncRoot)
            {
                return (double[])_weightsH1H2.Clone();
            }
        }
    }

    /// <summary>Gets the current training generation (for testing).</summary>
    internal int TrainingGeneration
    {
        get
        {
            lock (_syncRoot)
            {
                return _trainingGeneration;
            }
        }
    }

    /// <inheritdoc />
    public double Score(CandidateFeatures features)
    {
        if (_disposed)
        {
            return 0.5;
        }

        var vector = new double[CandidateFeatures.FeatureCount];
        features.WriteToVector(vector);

        // Thread-local scratch buffers: each thread gets its own set,
        // eliminating contention when scoring 1000+ candidates in parallel.
        _tlsH1Pre ??= new double[Hidden1Size];
        _tlsH1Act ??= new double[Hidden1Size];
        _tlsH2Pre ??= new double[Hidden2Size];
        _tlsH2Act ??= new double[Hidden2Size];

        try
        {
            _rwLock.EnterReadLock();

            if (_featureMeans is not null && _featureStdDevs is not null)
            {
                LearnedScoringStrategy.StandardizeSingleVector(vector, _featureMeans, _featureStdDevs);
            }

            return ForwardPass(
                vector,
                _weightsIH,
                _biasH1,
                _weightsH1H2,
                _biasH2,
                _weightsH2O,
                _biasOutput,
                _tlsH1Pre,
                _tlsH1Act,
                _tlsH2Pre,
                _tlsH2Act);
        }
        finally
        {
            if (_rwLock.IsReadLockHeld)
            {
                _rwLock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc />
    public ScoreExplanation ScoreWithExplanation(CandidateFeatures features)
    {
        if (_disposed)
        {
            return new ScoreExplanation { FinalScore = 0.5, StrategyName = Name };
        }

        var vector = new double[CandidateFeatures.FeatureCount];
        features.WriteToVector(vector);

        try
        {
            _rwLock.EnterReadLock();

            if (_featureMeans is not null && _featureStdDevs is not null)
            {
                LearnedScoringStrategy.StandardizeSingleVector(vector, _featureMeans, _featureStdDevs);
            }

            // Must allocate fresh buffers here (not shared scratch) because the
            // pre-activation values are needed after ForwardPass for gradient attribution.
            var h1Pre = new double[Hidden1Size];
            var h1Act = new double[Hidden1Size];
            var h2Pre = new double[Hidden2Size];
            var h2Act = new double[Hidden2Size];
            var score = ForwardPass(
                vector,
                _weightsIH,
                _biasH1,
                _weightsH1H2,
                _biasH2,
                _weightsH2O,
                _biasOutput,
                h1Pre,
                h1Act,
                h2Pre,
                h2Act);

            // Input-gradient attribution through both hidden layers:
            // contribution[i] = Σ_j Σ_k (wH2O[k] · relu'(h2Pre[k]) · wH1H2[k,j] · relu'(h1Pre[j]) · wIH[j,i]) · input[i]
            var inputSize = CandidateFeatures.FeatureCount;
            var attr = new double[inputSize];

            for (var k = 0; k < Hidden2Size; k++)
            {
                if (h2Pre[k] <= 0)
                {
                    continue;
                }

                var outW = _weightsH2O[k];
                for (var j = 0; j < Hidden1Size; j++)
                {
                    if (h1Pre[j] <= 0)
                    {
                        continue;
                    }

                    var h1h2W = _weightsH1H2[(k * Hidden1Size) + j];
                    var combined = outW * h1h2W;
                    var baseIdx = j * inputSize;
                    for (var i = 0; i < inputSize; i++)
                    {
                        attr[i] += combined * _weightsIH[baseIdx + i] * vector[i];
                    }
                }
            }

            var interactionContrib =
                attr[(int)FeatureIndex.GenreCountNormalized] +
                attr[(int)FeatureIndex.IsSeries] +
                attr[(int)FeatureIndex.GenreRatingInteraction] +
                attr[(int)FeatureIndex.GenreCollabInteraction] +
                attr[(int)FeatureIndex.CompletionRatio] +
                attr[(int)FeatureIndex.IsAbandoned] +
                attr[(int)FeatureIndex.HasInteraction] +
                attr[(int)FeatureIndex.SeriesProgressionBoost] +
                attr[(int)FeatureIndex.PopularityScore] +
                attr[(int)FeatureIndex.DayOfWeekAffinity];

            return new ScoreExplanation
            {
                FinalScore = score,
                GenreContribution = attr[(int)FeatureIndex.GenreSimilarity],
                CollaborativeContribution = attr[(int)FeatureIndex.CollaborativeScore],
                RatingContribution = attr[(int)FeatureIndex.RatingScore],
                RecencyContribution = attr[(int)FeatureIndex.RecencyScore],
                YearProximityContribution = attr[(int)FeatureIndex.YearProximityScore],
                UserRatingContribution = attr[(int)FeatureIndex.UserRatingScore],
                PeopleContribution = attr[(int)FeatureIndex.PeopleSimilarity],
                StudioContribution = attr[(int)FeatureIndex.StudioMatch],
                InteractionContribution = interactionContrib,
                GenrePenaltyMultiplier = 1.0,
                DominantSignal = ScoreExplanation.DetermineDominantSignal(
                    attr[(int)FeatureIndex.GenreSimilarity],
                    attr[(int)FeatureIndex.CollaborativeScore],
                    attr[(int)FeatureIndex.RatingScore],
                    attr[(int)FeatureIndex.UserRatingScore],
                    attr[(int)FeatureIndex.RecencyScore],
                    attr[(int)FeatureIndex.YearProximityScore],
                    interactionContrib,
                    attr[(int)FeatureIndex.PeopleSimilarity],
                    attr[(int)FeatureIndex.StudioMatch]),
                StrategyName = Name
            };
        }
        finally
        {
            if (_rwLock.IsReadLockHeld)
            {
                _rwLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    ///     Trains the MLP via backpropagation with Adam optimizer.
    /// </summary>
    /// <param name="examples">Training examples with features and labels.</param>
    /// <returns>True if training was performed, false if insufficient data.</returns>
    public bool Train(IReadOnlyList<TrainingExample> examples)
    {
        if (examples.Count < MinTrainingExamples)
        {
            return false;
        }

        var referenceTime = DateTime.UtcNow;
        var inputSize = CandidateFeatures.FeatureCount;

        var vectors = new double[examples.Count][];
        var weights = new double[examples.Count];

        for (var i = 0; i < examples.Count; i++)
        {
            vectors[i] = examples[i].Features.ToVector();
            weights[i] = examples[i].ComputeEffectiveWeight(referenceTime);
        }

        double[]? featureMeans = null;
        double[]? featureStdDevs = null;

        if (examples.Count >= MinExamplesForStandardization)
        {
            (featureMeans, featureStdDevs) = LearnedScoringStrategy.ComputeFeatureStatistics(vectors);
            LearnedScoringStrategy.StandardizeVectors(vectors, featureMeans, featureStdDevs);
        }

        try
        {
            _rwLock.EnterWriteLock();

            EnsureAdamState(inputSize);

            var valCount = Math.Max(MinValidationExamples, (int)(examples.Count * ValidationSplitRatio));
            valCount = Math.Min(valCount, examples.Count - MinTrainingExamples);
            var useEarlyStopping = valCount >= MinValidationExamples
                && examples.Count - valCount >= MinTrainingExamples;

            // Deterministic seed varies by generation to prevent identical train/val splits
            // across successive Train() calls while keeping results reproducible per generation.
            var rng = new Random(42 + _trainingGeneration);
            _trainingGeneration++;

            var indices = new int[examples.Count];
            for (var j = 0; j < indices.Length; j++)
            {
                indices[j] = j;
            }

            for (var j = indices.Length - 1; j > 0; j--)
            {
                var k = rng.Next(j + 1);
                (indices[j], indices[k]) = (indices[k], indices[j]);
            }

            int[] trainIdx;
            int[] valIdx;
            if (useEarlyStopping)
            {
                trainIdx = indices[..^valCount];
                valIdx = indices[^valCount..];
            }
            else
            {
                trainIdx = indices;
                valIdx = [];
            }

            var bestLoss = double.MaxValue;
            var patience = 0;

            var bestWIH = (double[])_weightsIH.Clone();
            var bestBH1 = (double[])_biasH1.Clone();
            var bestWH1H2 = (double[])_weightsH1H2.Clone();
            var bestBH2 = (double[])_biasH2.Clone();
            var bestWH2O = (double[])_weightsH2O.Clone();
            var bestBO = _biasOutput;

            var h1Pre = new double[Hidden1Size];
            var h1Act = new double[Hidden1Size];
            var h2Pre = new double[Hidden2Size];
            var h2Act = new double[Hidden2Size];
            var h1Err = new double[Hidden1Size];
            var h2Err = new double[Hidden2Size];

            var maxEpochs = useEarlyStopping ? MaxTrainingEpochs : Math.Min(MaxTrainingEpochs, MaxEpochsWithoutEarlyStopping);

            for (var epoch = 0; epoch < maxEpochs; epoch++)
            {
                for (var j = trainIdx.Length - 1; j > 0; j--)
                {
                    var k = rng.Next(j + 1);
                    (trainIdx[j], trainIdx[k]) = (trainIdx[k], trainIdx[j]);
                }

                foreach (var idx in trainIdx)
                {
                    var sw = weights[idx];
                    if (sw < MinSampleWeight)
                    {
                        continue;
                    }

                    var vec = vectors[idx];

                    var pred = ForwardPass(
                        vec,
                        _weightsIH,
                        _biasH1,
                        _weightsH1H2,
                        _biasH2,
                        _weightsH2O,
                        _biasOutput,
                        h1Pre,
                        h1Act,
                        h2Pre,
                        h2Act);

                    // Apply sigmoid derivative for correct backpropagation gradient:
                    // dL/dz = (pred - label) × sigmoid'(z) × sampleWeight
                    // where sigmoid'(z) = pred × (1 - pred)
                    var outErr = (pred - examples[idx].Label) * pred * (1.0 - pred) * sw;

                    _adamTimestep++;
                    var bc1 = 1.0 - Math.Pow(AdamBeta1, _adamTimestep);
                    var bc2 = 1.0 - Math.Pow(AdamBeta2, _adamTimestep);

                    // === Output layer Adam update (hidden2 → output) ===
                    for (var k = 0; k < Hidden2Size; k++)
                    {
                        var g = (outErr * h2Act[k]) + (L2Lambda * _weightsH2O[k]);
                        _mWH2O![k] = (AdamBeta1 * _mWH2O[k]) + ((1 - AdamBeta1) * g);
                        _vWH2O![k] = (AdamBeta2 * _vWH2O[k]) + ((1 - AdamBeta2) * g * g);
                        _weightsH2O[k] -= DefaultLearningRate * (_mWH2O[k] / bc1) / (Math.Sqrt(_vWH2O[k] / bc2) + AdamEpsilon);
                        _weightsH2O[k] = Math.Clamp(_weightsH2O[k], -WeightClamp, WeightClamp);
                    }

                    {
                        var g = outErr;
                        _mBO = (AdamBeta1 * _mBO) + ((1 - AdamBeta1) * g);
                        _vBO = (AdamBeta2 * _vBO) + ((1 - AdamBeta2) * g * g);
                        _biasOutput -= DefaultLearningRate * (_mBO / bc1) / (Math.Sqrt(_vBO / bc2) + AdamEpsilon);
                        _biasOutput = Math.Clamp(_biasOutput, -WeightClamp, WeightClamp);
                    }

                    // === Hidden2 layer error (backprop through ReLU) ===
                    for (var k = 0; k < Hidden2Size; k++)
                    {
                        h2Err[k] = h2Pre[k] > 0 ? outErr * _weightsH2O[k] : 0.0;
                    }

                    // === Hidden1→Hidden2 layer Adam update ===
                    for (var k = 0; k < Hidden2Size; k++)
                    {
                        var bIdx = k * Hidden1Size;
                        for (var j = 0; j < Hidden1Size; j++)
                        {
                            var p = bIdx + j;
                            var g = (h2Err[k] * h1Act[j]) + (L2Lambda * _weightsH1H2[p]);
                            _mWH1H2![p] = (AdamBeta1 * _mWH1H2[p]) + ((1 - AdamBeta1) * g);
                            _vWH1H2![p] = (AdamBeta2 * _vWH1H2[p]) + ((1 - AdamBeta2) * g * g);
                            _weightsH1H2[p] -= DefaultLearningRate * (_mWH1H2[p] / bc1) / (Math.Sqrt(_vWH1H2[p] / bc2) + AdamEpsilon);
                            _weightsH1H2[p] = Math.Clamp(_weightsH1H2[p], -WeightClamp, WeightClamp);
                        }

                        {
                            // No L2 regularization on bias terms
                            var g = h2Err[k];
                            _mBH2![k] = (AdamBeta1 * _mBH2[k]) + ((1 - AdamBeta1) * g);
                            _vBH2![k] = (AdamBeta2 * _vBH2[k]) + ((1 - AdamBeta2) * g * g);
                            _biasH2[k] -= DefaultLearningRate * (_mBH2[k] / bc1) / (Math.Sqrt(_vBH2[k] / bc2) + AdamEpsilon);
                            _biasH2[k] = Math.Clamp(_biasH2[k], -WeightClamp, WeightClamp);
                        }
                    }

                    // === Hidden1 layer error (backprop through ReLU from hidden2) ===
                    for (var j = 0; j < Hidden1Size; j++)
                    {
                        if (h1Pre[j] <= 0)
                        {
                            h1Err[j] = 0.0;
                            continue;
                        }

                        var sum = 0.0;
                        for (var k = 0; k < Hidden2Size; k++)
                        {
                            sum += h2Err[k] * _weightsH1H2[(k * Hidden1Size) + j];
                        }

                        h1Err[j] = sum;
                    }

                    // === Input→Hidden1 layer Adam update ===
                    for (var j = 0; j < Hidden1Size; j++)
                    {
                        var bIdx = j * inputSize;
                        for (var i = 0; i < inputSize; i++)
                        {
                            var p = bIdx + i;
                            var g = (h1Err[j] * vec[i]) + (L2Lambda * _weightsIH[p]);
                            _mWIH![p] = (AdamBeta1 * _mWIH[p]) + ((1 - AdamBeta1) * g);
                            _vWIH![p] = (AdamBeta2 * _vWIH[p]) + ((1 - AdamBeta2) * g * g);
                            _weightsIH[p] -= DefaultLearningRate * (_mWIH[p] / bc1) / (Math.Sqrt(_vWIH[p] / bc2) + AdamEpsilon);
                            _weightsIH[p] = Math.Clamp(_weightsIH[p], -WeightClamp, WeightClamp);
                        }

                        {
                            // No L2 regularization on bias terms
                            var g = h1Err[j];
                            _mBH1![j] = (AdamBeta1 * _mBH1[j]) + ((1 - AdamBeta1) * g);
                            _vBH1![j] = (AdamBeta2 * _vBH1[j]) + ((1 - AdamBeta2) * g * g);
                            _biasH1[j] -= DefaultLearningRate * (_mBH1[j] / bc1) / (Math.Sqrt(_vBH1[j] / bc2) + AdamEpsilon);
                            _biasH1[j] = Math.Clamp(_biasH1[j], -WeightClamp, WeightClamp);
                        }
                    }
                }

                if (useEarlyStopping && valIdx.Length > 0)
                {
                    var valLoss = ComputeMseLoss(examples, vectors, weights, valIdx);
                    if (valLoss < bestLoss - EarlyStoppingMinDelta)
                    {
                        bestLoss = valLoss;
                        patience = 0;
                        Array.Copy(_weightsIH, bestWIH, _weightsIH.Length);
                        Array.Copy(_biasH1, bestBH1, _biasH1.Length);
                        Array.Copy(_weightsH1H2, bestWH1H2, _weightsH1H2.Length);
                        Array.Copy(_biasH2, bestBH2, _biasH2.Length);
                        Array.Copy(_weightsH2O, bestWH2O, _weightsH2O.Length);
                        bestBO = _biasOutput;
                    }
                    else
                    {
                        patience++;
                        if (patience >= EarlyStoppingPatience)
                        {
                            Array.Copy(bestWIH, _weightsIH, _weightsIH.Length);
                            Array.Copy(bestBH1, _biasH1, _biasH1.Length);
                            Array.Copy(bestWH1H2, _weightsH1H2, _weightsH1H2.Length);
                            Array.Copy(bestBH2, _biasH2, _biasH2.Length);
                            Array.Copy(bestWH2O, _weightsH2O, _weightsH2O.Length);
                            _biasOutput = bestBO;
                            break;
                        }
                    }
                }
            }

            _lastValidationLoss = bestLoss < double.MaxValue
                ? bestLoss
                : ComputeMseLoss(examples, vectors, weights, trainIdx);

            _featureMeans = featureMeans;
            _featureStdDevs = featureStdDevs;

            // Persist inside the write lock so that no concurrent Score() call can observe
            // a window between training completion and save snapshot.
            TrySaveWeights();

            LogFeatureImportance(inputSize);
        }
        finally
        {
            if (_rwLock.IsWriteLockHeld)
            {
                _rwLock.ExitWriteLock();
            }
        }

        return true;
    }

    /// <summary>
    ///     MLP forward pass: input → hidden₁ (ReLU) → hidden₂ (ReLU) → output (Sigmoid).
    ///     Uses pre-allocated buffers for hidden activations to avoid allocation.
    /// </summary>
    /// <param name="input">Input feature vector [InputSize].</param>
    /// <param name="wIH">Input→Hidden1 weights [Hidden1Size × InputSize] row-major.</param>
    /// <param name="bH1">Hidden1 biases [Hidden1Size].</param>
    /// <param name="wH1H2">Hidden1→Hidden2 weights [Hidden2Size × Hidden1Size] row-major.</param>
    /// <param name="bH2">Hidden2 biases [Hidden2Size].</param>
    /// <param name="wH2O">Hidden2→Output weights [Hidden2Size].</param>
    /// <param name="bO">Output bias scalar.</param>
    /// <param name="h1Pre">Pre-allocated buffer for hidden1 pre-activation values [Hidden1Size].</param>
    /// <param name="h1Act">Pre-allocated buffer for hidden1 post-activation values [Hidden1Size].</param>
    /// <param name="h2Pre">Pre-allocated buffer for hidden2 pre-activation values [Hidden2Size].</param>
    /// <param name="h2Act">Pre-allocated buffer for hidden2 post-activation values [Hidden2Size].</param>
    /// <returns>Output score in [0, 1] via sigmoid.</returns>
    internal static double ForwardPass(
        double[] input,
        double[] wIH,
        double[] bH1,
        double[] wH1H2,
        double[] bH2,
        double[] wH2O,
        double bO,
        double[] h1Pre,
        double[] h1Act,
        double[] h2Pre,
        double[] h2Act)
    {
        var inputSize = input.Length;

        // Hidden layer 1: input → hidden1 (ReLU)
        for (var j = 0; j < Hidden1Size; j++)
        {
            var sum = bH1[j];
            var baseIdx = j * inputSize;
            for (var i = 0; i < inputSize; i++)
            {
                sum += wIH[baseIdx + i] * input[i];
            }

            h1Pre[j] = sum;
            h1Act[j] = sum > 0 ? sum : 0.0;
        }

        // Hidden layer 2: hidden1 → hidden2 (ReLU)
        for (var k = 0; k < Hidden2Size; k++)
        {
            var sum = bH2[k];
            var baseIdx = k * Hidden1Size;
            for (var j = 0; j < Hidden1Size; j++)
            {
                sum += wH1H2[baseIdx + j] * h1Act[j];
            }

            h2Pre[k] = sum;
            h2Act[k] = sum > 0 ? sum : 0.0;
        }

        // Output layer: hidden2 → output (Sigmoid)
        var outputZ = bO;
        for (var k = 0; k < Hidden2Size; k++)
        {
            outputZ += wH2O[k] * h2Act[k];
        }

        return Sigmoid(outputZ);
    }

    /// <summary>
    ///     Numerically stable sigmoid: 1 / (1 + exp(-x)).
    ///     Guards against overflow for large |x|.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>The sigmoid output in (0, 1).</returns>
    internal static double Sigmoid(double x)
    {
        if (x >= 0)
        {
            var ez = Math.Exp(-x);
            return 1.0 / (1.0 + ez);
        }
        else
        {
            var ez = Math.Exp(x);
            return ez / (1.0 + ez);
        }
    }

    /// <summary>
    ///     Xavier/Glorot uniform initialization for stable gradient flow.
    ///     Each layer's weights ~ U(-limit, limit) where limit = sqrt(6 / (fan_in + fan_out)).
    /// </summary>
    private void InitializeXavier(int inputSize)
    {
        var rng = new Random(42);

        // Input → Hidden1
        var limitIH = Math.Sqrt(6.0 / (inputSize + Hidden1Size));
        for (var i = 0; i < _weightsIH.Length; i++)
        {
            _weightsIH[i] = (rng.NextDouble() * 2.0 * limitIH) - limitIH;
        }

        // Hidden1 → Hidden2
        var limitH1H2 = Math.Sqrt(6.0 / (Hidden1Size + Hidden2Size));
        for (var i = 0; i < _weightsH1H2.Length; i++)
        {
            _weightsH1H2[i] = (rng.NextDouble() * 2.0 * limitH1H2) - limitH1H2;
        }

        // Hidden2 → Output
        var limitH2O = Math.Sqrt(6.0 / (Hidden2Size + 1));
        for (var i = 0; i < _weightsH2O.Length; i++)
        {
            _weightsH2O[i] = (rng.NextDouble() * 2.0 * limitH2O) - limitH2O;
        }

        Array.Clear(_biasH1);
        Array.Clear(_biasH2);
    }

    /// <summary>
    ///     Ensures Adam optimizer moment arrays are allocated.
    ///     Only allocates once; subsequent calls are no-ops.
    /// </summary>
    private void EnsureAdamState(int inputSize)
    {
        var wihLen = Hidden1Size * inputSize;
        if (_mWIH is not null && _mWIH.Length == wihLen)
        {
            return;
        }

        // Input → Hidden1
        _mWIH = new double[wihLen];
        _vWIH = new double[wihLen];
        _mBH1 = new double[Hidden1Size];
        _vBH1 = new double[Hidden1Size];

        // Hidden1 → Hidden2
        var wh1h2Len = Hidden2Size * Hidden1Size;
        _mWH1H2 = new double[wh1h2Len];
        _vWH1H2 = new double[wh1h2Len];
        _mBH2 = new double[Hidden2Size];
        _vBH2 = new double[Hidden2Size];

        // Hidden2 → Output
        _mWH2O = new double[Hidden2Size];
        _vWH2O = new double[Hidden2Size];
        _mBO = 0;
        _vBO = 0;

        _adamTimestep = 0;
    }

    /// <summary>
    ///     Computes weighted MSE loss on a subset of examples.
    /// </summary>
    private double ComputeMseLoss(
        IReadOnlyList<TrainingExample> examples,
        double[][] vectors,
        double[] effectiveWeights,
        int[] indices)
    {
        var totalLoss = 0.0;
        var totalWeight = 0.0;
        var h1Pre = new double[Hidden1Size];
        var h1Act = new double[Hidden1Size];
        var h2Pre = new double[Hidden2Size];
        var h2Act = new double[Hidden2Size];

        foreach (var idx in indices)
        {
            var pred = ForwardPass(
                vectors[idx],
                _weightsIH,
                _biasH1,
                _weightsH1H2,
                _biasH2,
                _weightsH2O,
                _biasOutput,
                h1Pre,
                h1Act,
                h2Pre,
                h2Act);
            var error = pred - examples[idx].Label;
            var w = effectiveWeights[idx];
            totalLoss += w * error * error;
            totalWeight += w;
        }

        return totalWeight > 0 ? totalLoss / totalWeight : 0.0;
    }

    /// <summary>Tries to load persisted weights from disk.</summary>
    private void TryLoadWeights()
    {
        if (string.IsNullOrEmpty(_weightsPath) || !File.Exists(_weightsPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_weightsPath);
            var data = JsonSerializer.Deserialize<NeuralWeightsData>(json);
            if (data is not null
                && data.Version == CurrentWeightsVersion
                && data.WeightsIH?.Length == Hidden1Size * CandidateFeatures.FeatureCount
                && data.BiasH1 is { Length: Hidden1Size }
                && data.WeightsH1H2?.Length == Hidden2Size * Hidden1Size
                && data.BiasH2 is { Length: Hidden2Size }
                && data.WeightsH2O is { Length: Hidden2Size })
            {
                _weightsIH = data.WeightsIH;
                _biasH1 = data.BiasH1;
                _weightsH1H2 = data.WeightsH1H2;
                _biasH2 = data.BiasH2;
                _weightsH2O = data.WeightsH2O;
                _biasOutput = data.BiasOutput;
                _featureMeans = data.FeatureMeans;
                _featureStdDevs = data.FeatureStdDevs;
                _trainingGeneration = data.TrainingGeneration;

                // Reset Adam timestep: the moment arrays (m/v) are NOT persisted,
                // so restoring a high timestep with zero moments would cause incorrect
                // bias correction factors (bc1/bc2 ≈ 1.0 with zero numerators).
                // Starting fresh ensures Adam's adaptive learning rate works correctly.
                _adamTimestep = 0;
            }
            else if (data is not null)
            {
                _logger?.LogWarning(
                    "NeuralScoringStrategy: Discarding persisted weights (version={FileVersion}, expected={ExpectedVersion}). Resetting to defaults",
                    data.Version,
                    CurrentWeightsVersion);
            }
        }
        catch (IOException ex)
        {
            _logger?.LogWarning(ex, "NeuralScoringStrategy: Failed to load weights");
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "NeuralScoringStrategy: Failed to parse weights");
        }
    }

    /// <summary>Persists current weights to disk atomically. Must be called under write lock or during init.</summary>
    private void TrySaveWeights()
    {
        if (string.IsNullOrEmpty(_weightsPath))
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_weightsPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Caller must hold the write lock (or be in constructor before any concurrent access).
            // Snapshot the weights under that protection.
            var data = new NeuralWeightsData
            {
                WeightsIH = (double[])_weightsIH.Clone(),
                BiasH1 = (double[])_biasH1.Clone(),
                WeightsH1H2 = (double[])_weightsH1H2.Clone(),
                BiasH2 = (double[])_biasH2.Clone(),
                WeightsH2O = (double[])_weightsH2O.Clone(),
                BiasOutput = _biasOutput,
                FeatureMeans = _featureMeans is not null ? (double[])_featureMeans.Clone() : null,
                FeatureStdDevs = _featureStdDevs is not null ? (double[])_featureStdDevs.Clone() : null,
                TrainingGeneration = _trainingGeneration,
                UpdatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Version = CurrentWeightsVersion
            };
            var json = JsonSerializer.Serialize(data, SerializerOptions);

            var tempPath = _weightsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _weightsPath, overwrite: true);
        }
        catch (IOException ex)
        {
            _logger?.LogWarning(ex, "NeuralScoringStrategy: Failed to save weights");
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "NeuralScoringStrategy: Failed to serialize weights");
        }
    }

    /// <summary>
    ///     Logs per-feature importance based on input→hidden1 weight L2 norms.
    ///     Importance[f] = sqrt(Σ_j weightsIH[j, f]²) — measures how strongly
    ///     each input feature drives hidden layer activations.
    ///     Must be called under write lock.
    /// </summary>
    private void LogFeatureImportance(int inputSize)
    {
        if (_logger is null || !_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var featureNames = Enum.GetNames<FeatureIndex>();
        var importances = new double[inputSize];

        for (var f = 0; f < inputSize; f++)
        {
            var sumSq = 0.0;
            for (var j = 0; j < Hidden1Size; j++)
            {
                var w = _weightsIH[(j * inputSize) + f];
                sumSq += w * w;
            }

            importances[f] = Math.Sqrt(sumSq);
        }

        // Sort by importance descending for readability
        var ranked = new (string Name, double Importance)[inputSize];
        for (var i = 0; i < inputSize; i++)
        {
            ranked[i] = (i < featureNames.Length ? featureNames[i] : $"Feature{i}", importances[i]);
        }

        Array.Sort(ranked, (a, b) => b.Importance.CompareTo(a.Importance));

        var parts = new string[ranked.Length];
        for (var i = 0; i < ranked.Length; i++)
        {
            parts[i] = string.Format(CultureInfo.InvariantCulture, "{0}={1:F4}", ranked[i].Name, ranked[i].Importance);
        }

        _logger.LogDebug("NeuralScoringStrategy feature importance (L2 norm): {FeatureImportance}", string.Join(", ", parts));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _rwLock.Dispose();
    }

    /// <summary>Serializable container for persisted neural network weights.</summary>
    internal sealed class NeuralWeightsData
    {
        /// <summary>Gets or sets the input→hidden1 weights [Hidden1Size × InputSize].</summary>
        public double[] WeightsIH { get; set; } = [];

        /// <summary>Gets or sets the hidden1 biases [Hidden1Size].</summary>
        public double[] BiasH1 { get; set; } = [];

        /// <summary>Gets or sets the hidden1→hidden2 weights [Hidden2Size × Hidden1Size].</summary>
        public double[] WeightsH1H2 { get; set; } = [];

        /// <summary>Gets or sets the hidden2 biases [Hidden2Size].</summary>
        public double[] BiasH2 { get; set; } = [];

        /// <summary>Gets or sets the hidden2→output weights [Hidden2Size].</summary>
        public double[] WeightsH2O { get; set; } = [];

        /// <summary>Gets or sets the output bias.</summary>
        public double BiasOutput { get; set; }

        /// <summary>Gets or sets the per-feature means for Z-score standardization.</summary>
        public double[]? FeatureMeans { get; set; }

        /// <summary>Gets or sets the per-feature standard deviations for Z-score standardization.</summary>
        public double[]? FeatureStdDevs { get; set; }

        /// <summary>Gets or sets the training generation counter.</summary>
        public int TrainingGeneration { get; set; }

        // AdamTimestep was previously persisted here but never loaded (moments are not
        // persisted, so restoring the timestep alone is meaningless). Removed as dead code.

        /// <summary>Gets or sets the ISO 8601 timestamp of the last update.</summary>
        public string UpdatedAt { get; set; } = string.Empty;

        /// <summary>Gets or sets the schema version.</summary>
        public int Version { get; set; }
    }
}