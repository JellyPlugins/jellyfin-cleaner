using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation;

/// <summary>
///     Ensemble scoring strategy that combines the learned (adaptive ML) strategy
///     with the heuristic (rule-based) strategy for more robust recommendations.
///     Uses a dynamic blending factor that shifts weight toward the learned model
///     as more training data becomes available.
/// </summary>
/// <remarks>
///     Architecture: score = α × Learned.Score + (1 - α) × Heuristic.Score
///     where α increases with the number of training examples:
///       - Few examples (&lt; 20): α = 0.3 (heuristic dominates)
///       - Medium (20–100): α = 0.6 (balanced)
///       - Many (&gt; 100): α = 0.8 (learned dominates, heuristic is safety net)
///     Training delegates to the learned strategy; the heuristic strategy is static.
/// </remarks>
public sealed class EnsembleScoringStrategy : IScoringStrategy
{
    /// <summary>Learned weight when few training examples are available (&lt; 20).</summary>
    internal const double AlphaLow = 0.3;

    /// <summary>Learned weight when a moderate amount of training data exists (20–100).</summary>
    internal const double AlphaMedium = 0.6;

    /// <summary>Learned weight when abundant training data exists (&gt; 100).</summary>
    internal const double AlphaHigh = 0.8;

    /// <summary>Training example count threshold for transitioning from low to medium alpha.</summary>
    internal const int MediumDataThreshold = 20;

    /// <summary>Training example count threshold for transitioning from medium to high alpha.</summary>
    internal const int HighDataThreshold = 100;

    /// <summary>Cached JSON serializer options for ensemble state persistence.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly HeuristicScoringStrategy _heuristic;
    private readonly LearnedScoringStrategy _learned;
    private readonly string? _statePath;
    private double _alpha = AlphaLow;
    private int _trainingExampleCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EnsembleScoringStrategy" /> class.
    /// </summary>
    /// <param name="weightsPath">
    ///     Optional file path for persisting learned weights.
    ///     Passed through to the underlying <see cref="LearnedScoringStrategy" />.
    /// </param>
    public EnsembleScoringStrategy(string? weightsPath = null)
    {
        _learned = new LearnedScoringStrategy(weightsPath);
        _heuristic = new HeuristicScoringStrategy();

        // Derive ensemble state path from the learned weights path
        if (!string.IsNullOrEmpty(weightsPath))
        {
            _statePath = Path.Combine(
                Path.GetDirectoryName(weightsPath) ?? string.Empty,
                "ensemble_state.json");
        }

        TryLoadState();
    }

    /// <inheritdoc />
    public string Name => "Ensemble (Adaptive ML + Rules)";

    /// <inheritdoc />
    public string NameKey => "strategyEnsemble";

    /// <summary>
    ///     Gets the current blending factor α (for testing/debugging).
    ///     α = weight of the learned strategy; (1 - α) = weight of the heuristic strategy.
    /// </summary>
    internal double CurrentAlpha => _alpha;

    /// <summary>
    ///     Gets the total number of training examples seen so far (for testing/debugging).
    /// </summary>
    internal int TrainingExampleCount => _trainingExampleCount;

    /// <summary>
    ///     Gets the underlying learned strategy (for testing/debugging).
    /// </summary>
    internal LearnedScoringStrategy LearnedStrategy => _learned;

    /// <summary>
    ///     Gets the underlying heuristic strategy (for testing/debugging).
    /// </summary>
    internal HeuristicScoringStrategy HeuristicStrategy => _heuristic;

    /// <inheritdoc />
    public double Score(CandidateFeatures features)
    {
        var learnedScore = _learned.Score(features);
        var heuristicScore = _heuristic.Score(features);

        // Weighted blend: α × learned + (1 - α) × heuristic
        return (_alpha * learnedScore) + ((1.0 - _alpha) * heuristicScore);
    }

    /// <inheritdoc />
    public bool Train(IReadOnlyList<TrainingExample> examples)
    {
        var result = _learned.Train(examples);

        if (result)
        {
            // Track cumulative training examples to adjust blending factor
            _trainingExampleCount += examples.Count;
            _alpha = _trainingExampleCount switch
            {
                >= HighDataThreshold => AlphaHigh,
                >= MediumDataThreshold => AlphaMedium,
                _ => AlphaLow
            };

            TrySaveState();
        }

        return result;
    }

    /// <summary>
    ///     Tries to load persisted ensemble state (alpha, training count) from disk.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:DoNotCatchGeneralExceptionTypes",
        Justification = "Graceful fallback to defaults on any I/O or parse error")]
    private void TryLoadState()
    {
        if (string.IsNullOrEmpty(_statePath) || !File.Exists(_statePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            var data = JsonSerializer.Deserialize<EnsembleStateData>(json);
            if (data is not null && data.TrainingExampleCount > 0)
            {
                _trainingExampleCount = data.TrainingExampleCount;
                _alpha = _trainingExampleCount switch
                {
                    >= HighDataThreshold => AlphaHigh,
                    >= MediumDataThreshold => AlphaMedium,
                    _ => AlphaLow
                };
            }
        }
        catch (Exception)
        {
            // Silently fall back to defaults
        }
    }

    /// <summary>
    ///     Tries to persist current ensemble state to disk.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:DoNotCatchGeneralExceptionTypes",
        Justification = "Non-critical persistence — silently ignore write failures")]
    private void TrySaveState()
    {
        if (string.IsNullOrEmpty(_statePath))
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var data = new EnsembleStateData
            {
                TrainingExampleCount = _trainingExampleCount,
                Alpha = _alpha,
                UpdatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };

            var json = JsonSerializer.Serialize(data, SerializerOptions);
            var tempPath = _statePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _statePath, overwrite: true);
        }
        catch (Exception)
        {
            // Non-critical — silently ignore
        }
    }

    /// <summary>
    ///     Serializable container for persisted ensemble state.
    /// </summary>
    internal sealed class EnsembleStateData
    {
        /// <summary>Gets or sets the cumulative number of training examples seen.</summary>
        public int TrainingExampleCount { get; set; }

        /// <summary>Gets or sets the current blending factor alpha.</summary>
        public double Alpha { get; set; }

        /// <summary>Gets or sets the ISO 8601 timestamp of the last update.</summary>
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
