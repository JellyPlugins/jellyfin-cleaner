using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation;

/// <summary>
///     Adaptive ML scoring strategy using a linear model with learned weights.
///     Learns personalized feature weights from user watch history via mini-batch gradient descent.
///     Genre-mismatch penalties are NOT applied here — they are handled centrally by the
///     ensemble layer to avoid double-penalization.
///     No external ML dependencies required — pure C# implementation.
/// </summary>
/// <remarks>
///     Architecture: 11 input features → 11 weights + 1 bias → clamp(0,1) → score (0–1).
///     Features include 2 interaction terms (genre×rating, genre×collab).
///     Training uses mean squared error (MSE) loss with L2 regularization, sample weighting
///     (temporal decay), optional Z-score feature standardization, and early stopping.
///     Weights are persisted to disk so they survive server restarts.
/// </remarks>
public sealed class LearnedScoringStrategy : IScoringStrategy
{
    /// <summary>Default learning rate for gradient descent.</summary>
    internal const double DefaultLearningRate = 0.02;

    /// <summary>L2 regularization strength (weight decay).</summary>
    internal const double L2Lambda = 0.001;

    /// <summary>Maximum number of training epochs per <see cref="Train"/> call.</summary>
    internal const int MaxTrainingEpochs = 30;

    /// <summary>Minimum number of training examples required before training runs.</summary>
    internal const int MinTrainingExamples = 5;

    /// <summary>Number of consecutive epochs without improvement before early stopping triggers.</summary>
    internal const int EarlyStoppingPatience = 3;

    /// <summary>Minimum fraction of examples used for validation (rest is training).</summary>
    internal const double ValidationSplitRatio = 0.2;

    /// <summary>Minimum number of validation examples required for early stopping.</summary>
    internal const int MinValidationExamples = 2;

    /// <summary>
    ///     Minimum number of examples before Z-score standardization is applied.
    ///     Below this threshold, raw features are used to avoid unstable statistics.
    /// </summary>
    internal const int MinExamplesForStandardization = 10;

    /// <summary>
    ///     Current schema version for persisted weights. Increment when the feature set or
    ///     weight semantics change so that stale weights are discarded on load.
    /// </summary>
    internal const int CurrentWeightsVersion = 5;

    /// <summary>Cached JSON serializer options for weight persistence.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly object _syncRoot = new();
    private readonly string? _weightsPath;
    private double _bias;
    private double _lastValidationLoss = double.MaxValue;
    private double[] _weights;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LearnedScoringStrategy" /> class
    ///     with default initial weights optimized for genre-driven recommendations.
    /// </summary>
    /// <param name="weightsPath">
    ///     Optional file path for persisting learned weights.
    ///     If null, weights are kept in memory only.
    /// </param>
    public LearnedScoringStrategy(string? weightsPath = null)
    {
        _weightsPath = weightsPath;

        // Initialize with genre-dominant weights — genre match is the strongest signal
        _weights = DefaultWeights.CreateWeightArray();
        _bias = DefaultWeights.Bias; // slight positive bias so perfect matches approach 1.0

        // Try to load persisted weights
        TryLoadWeights();
    }

    /// <inheritdoc />
    public string Name => "Learned (Adaptive ML)";

    /// <inheritdoc />
    public string NameKey => "strategyLearned";

    /// <summary>
    ///     Gets the validation loss from the last training run.
    ///     Used by <see cref="EnsembleScoringStrategy"/> to gate alpha progression.
    ///     Returns <see cref="double.MaxValue"/> if no training has been performed.
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

    /// <summary>
    ///     Gets a copy of the current weights (for testing/debugging).
    /// </summary>
    internal double[] CurrentWeights
    {
        get
        {
            lock (_syncRoot)
            {
                return (double[])_weights.Clone();
            }
        }
    }

    /// <summary>
    ///     Gets the current bias value (for testing/debugging).
    /// </summary>
    internal double CurrentBias
    {
        get
        {
            lock (_syncRoot)
            {
                return _bias;
            }
        }
    }

    /// <inheritdoc />
    public double Score(CandidateFeatures features)
    {
        return ScoreWithExplanation(features).FinalScore;
    }

    /// <inheritdoc />
    public ScoreExplanation ScoreWithExplanation(CandidateFeatures features)
    {
        var vector = features.ToVector();
        ValidateVectorLength(vector);

        lock (_syncRoot)
        {
            return ScoringHelper.BuildExplanation(vector, _weights, _bias, Name);
        }
    }

    /// <inheritdoc />
    public bool Train(IReadOnlyList<TrainingExample> examples)
    {
        if (examples.Count < MinTrainingExamples)
        {
            return false;
        }

        // Pre-compute all feature vectors ONCE before training (Point 4)
        // Also pre-compute effective weights for temporal decay (Point 6)
        var precomputedVectors = new double[examples.Count][];
        var effectiveWeights = new double[examples.Count];

        for (var i = 0; i < examples.Count; i++)
        {
            precomputedVectors[i] = examples[i].Features.ToVector();
            effectiveWeights[i] = examples[i].ComputeEffectiveWeight();
        }

        // Optional Z-score standardization (Point 7)
        double[]? featureMeans = null;
        double[]? featureStdDevs = null;

        if (examples.Count >= MinExamplesForStandardization)
        {
            (featureMeans, featureStdDevs) = ComputeFeatureStatistics(precomputedVectors);
            StandardizeVectors(precomputedVectors, featureMeans, featureStdDevs);
        }

        lock (_syncRoot)
        {
            // Split into training and validation sets for early stopping
            var validationCount = Math.Max(MinValidationExamples, (int)(examples.Count * ValidationSplitRatio));
            validationCount = Math.Min(validationCount, examples.Count - MinTrainingExamples);

            // If we can't split properly, train on all data without early stopping
            var useEarlyStopping = validationCount >= MinValidationExamples
                && examples.Count - validationCount >= MinTrainingExamples;

            var rng = new Random();

            // Create shuffled index array for split
            var allIndices = new int[examples.Count];
            for (var j = 0; j < allIndices.Length; j++)
            {
                allIndices[j] = j;
            }

            // Fisher-Yates shuffle for random split
            for (var j = allIndices.Length - 1; j > 0; j--)
            {
                var k = rng.Next(j + 1);
                (allIndices[j], allIndices[k]) = (allIndices[k], allIndices[j]);
            }

            int[] trainIndices;
            int[] valIndices;

            if (useEarlyStopping)
            {
                trainIndices = allIndices[..^validationCount];
                valIndices = allIndices[^validationCount..];
            }
            else
            {
                trainIndices = allIndices;
                valIndices = [];
            }

            var bestLoss = double.MaxValue;
            var patienceCounter = 0;
            var bestWeights = (double[])_weights.Clone();
            var bestBias = _bias;

            var maxEpochs = useEarlyStopping ? MaxTrainingEpochs : Math.Min(MaxTrainingEpochs, 15);

            for (var epoch = 0; epoch < maxEpochs; epoch++)
            {
                // Fisher-Yates shuffle training indices each epoch
                for (var j = trainIndices.Length - 1; j > 0; j--)
                {
                    var k = rng.Next(j + 1);
                    (trainIndices[j], trainIndices[k]) = (trainIndices[k], trainIndices[j]);
                }

                foreach (var idx in trainIndices)
                {
                    var vector = precomputedVectors[idx];
                    var sampleWeight = effectiveWeights[idx];

                    // Skip examples with negligible weight (very old data)
                    if (sampleWeight < 0.01)
                    {
                        continue;
                    }

                    // Forward pass — linear model
                    var z = ScoringHelper.ComputeRawScore(vector, _weights, _bias);
                    var predicted = Math.Clamp(z, 0.0, 1.0);

                    // Error = predicted - label (gradient of MSE loss), weighted by sample importance
                    var error = (predicted - examples[idx].Label) * sampleWeight;

                    // Only update if not clamped (sub-gradient: skip when at boundary moving wrong way)
                    if ((z <= 0 && error < 0) || (z >= 1 && error > 0))
                    {
                        continue;
                    }

                    // Update weights with gradient descent + L2 regularization
                    var len = Math.Min(vector.Length, _weights.Length);
                    for (var i = 0; i < len; i++)
                    {
                        var gradient = (error * vector[i]) + (L2Lambda * _weights[i]);
                        _weights[i] -= DefaultLearningRate * gradient;
                        _weights[i] = Math.Clamp(_weights[i], -2.0, 2.0);
                    }

                    // Update bias (no regularization on bias)
                    _bias -= DefaultLearningRate * error;
                    _bias = Math.Clamp(_bias, -1.0, 1.0);
                }

                // Early stopping: evaluate on validation set
                if (useEarlyStopping && valIndices.Length > 0)
                {
                    var valLoss = ComputeMseLoss(examples, precomputedVectors, effectiveWeights, valIndices, _weights, _bias);

                    if (valLoss < bestLoss - 1e-6)
                    {
                        bestLoss = valLoss;
                        patienceCounter = 0;
                        Array.Copy(_weights, bestWeights, _weights.Length);
                        bestBias = _bias;
                    }
                    else
                    {
                        patienceCounter++;
                        if (patienceCounter >= EarlyStoppingPatience)
                        {
                            // Restore best weights
                            Array.Copy(bestWeights, _weights, _weights.Length);
                            _bias = bestBias;
                            break;
                        }
                    }
                }
            }

            // Store validation loss for ensemble alpha gating (Point 5)
            _lastValidationLoss = bestLoss < double.MaxValue ? bestLoss : ComputeTrainingLoss(examples, precomputedVectors, effectiveWeights, _weights, _bias);
        }

        // Persist updated weights
        TrySaveWeights();
        return true;
    }

    /// <summary>
    ///     Computes Z-score statistics (mean, stddev) for each feature across all training vectors.
    /// </summary>
    /// <param name="vectors">The pre-computed feature vectors.</param>
    /// <returns>A tuple of (means, stdDevs) arrays indexed by feature.</returns>
    internal static (double[] Means, double[] StdDevs) ComputeFeatureStatistics(double[][] vectors)
    {
        var featureCount = CandidateFeatures.FeatureCount;
        var means = new double[featureCount];
        var stdDevs = new double[featureCount];
        var n = vectors.Length;

        if (n == 0)
        {
            return (means, stdDevs);
        }

        // Compute means
        for (var i = 0; i < n; i++)
        {
            for (var f = 0; f < featureCount; f++)
            {
                means[f] += vectors[i][f];
            }
        }

        for (var f = 0; f < featureCount; f++)
        {
            means[f] /= n;
        }

        // Compute standard deviations
        for (var i = 0; i < n; i++)
        {
            for (var f = 0; f < featureCount; f++)
            {
                var diff = vectors[i][f] - means[f];
                stdDevs[f] += diff * diff;
            }
        }

        for (var f = 0; f < featureCount; f++)
        {
            stdDevs[f] = Math.Sqrt(stdDevs[f] / n);
        }

        return (means, stdDevs);
    }

    /// <summary>
    ///     Standardizes feature vectors in-place using Z-score normalization.
    ///     Features with zero or near-zero standard deviation are left unchanged.
    /// </summary>
    /// <param name="vectors">The feature vectors to standardize (modified in-place).</param>
    /// <param name="means">The per-feature means.</param>
    /// <param name="stdDevs">The per-feature standard deviations.</param>
    internal static void StandardizeVectors(double[][] vectors, double[] means, double[] stdDevs)
    {
        var featureCount = means.Length;
        for (var i = 0; i < vectors.Length; i++)
        {
            for (var f = 0; f < featureCount; f++)
            {
                if (stdDevs[f] > 1e-8)
                {
                    vectors[i][f] = (vectors[i][f] - means[f]) / stdDevs[f];
                }
            }
        }
    }

    /// <summary>
    ///     Computes the weighted mean squared error loss on a subset of examples.
    /// </summary>
    private static double ComputeMseLoss(
        IReadOnlyList<TrainingExample> examples,
        double[][] precomputedVectors,
        double[] effectiveWeights,
        int[] indices,
        double[] weights,
        double bias)
    {
        var totalLoss = 0.0;
        var totalWeight = 0.0;

        foreach (var idx in indices)
        {
            var predicted = Math.Clamp(ScoringHelper.ComputeRawScore(precomputedVectors[idx], weights, bias), 0.0, 1.0);
            var error = predicted - examples[idx].Label;
            var w = effectiveWeights[idx];
            totalLoss += w * error * error;
            totalWeight += w;
        }

        return totalWeight > 0 ? totalLoss / totalWeight : 0.0;
    }

    /// <summary>
    ///     Computes the weighted training loss across all examples (used when no validation split).
    /// </summary>
    private static double ComputeTrainingLoss(
        IReadOnlyList<TrainingExample> examples,
        double[][] precomputedVectors,
        double[] effectiveWeights,
        double[] weights,
        double bias)
    {
        var allIndices = new int[examples.Count];
        for (var i = 0; i < allIndices.Length; i++)
        {
            allIndices[i] = i;
        }

        return ComputeMseLoss(examples, precomputedVectors, effectiveWeights, allIndices, weights, bias);
    }

    /// <summary>
    ///     Validates that a feature vector has the expected length.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the vector length doesn't match the expected feature count.</exception>
    private static void ValidateVectorLength(double[] vector)
    {
        Debug.Assert(
            vector.Length == CandidateFeatures.FeatureCount,
            $"Feature vector length mismatch: expected {CandidateFeatures.FeatureCount}, got {vector.Length}");

        if (vector.Length != CandidateFeatures.FeatureCount)
        {
            throw new ArgumentException(
                $"Feature vector length mismatch: expected {CandidateFeatures.FeatureCount}, got {vector.Length}",
                nameof(vector));
        }
    }

    /// <summary>
    ///     Tries to load persisted weights from disk.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:DoNotCatchGeneralExceptionTypes",
        Justification = "Graceful fallback to default weights on any I/O or parse error")]
    private void TryLoadWeights()
    {
        if (string.IsNullOrEmpty(_weightsPath) || !File.Exists(_weightsPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_weightsPath);
            var data = JsonSerializer.Deserialize<WeightsData>(json);
            if (data?.Weights is { Length: CandidateFeatures.FeatureCount }
                && data.Version == CurrentWeightsVersion)
            {
                _weights = data.Weights;
                _bias = data.Bias;
            }
        }
        catch (Exception)
        {
            // Silently fall back to default weights
        }
    }

    /// <summary>
    ///     Tries to persist current weights to disk.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:DoNotCatchGeneralExceptionTypes",
        Justification = "Non-critical persistence — silently ignore write failures")]
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

            double[] weightsSnapshot;
            double biasSnapshot;

            lock (_syncRoot)
            {
                weightsSnapshot = (double[])_weights.Clone();
                biasSnapshot = _bias;
            }

            var data = new WeightsData
            {
                Weights = weightsSnapshot,
                Bias = biasSnapshot,
                UpdatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Version = CurrentWeightsVersion
            };

            var json = JsonSerializer.Serialize(data, SerializerOptions);
            var tempPath = _weightsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _weightsPath, overwrite: true);
        }
        catch (Exception)
        {
            // Non-critical — silently ignore
        }
    }

    /// <summary>
    ///     Serializable container for persisted weights.
    /// </summary>
    internal sealed class WeightsData
    {
        /// <summary>Gets or sets the feature weights array.</summary>
        public double[] Weights { get; set; } = [];

        /// <summary>Gets or sets the bias term.</summary>
        public double Bias { get; set; }

        /// <summary>Gets or sets the ISO 8601 timestamp of the last update.</summary>
        public string UpdatedAt { get; set; } = string.Empty;

        /// <summary>Gets or sets the schema version.</summary>
        public int Version { get; set; }
    }
}