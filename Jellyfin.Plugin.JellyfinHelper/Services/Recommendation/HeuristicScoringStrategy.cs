using System.Collections.Generic;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation;

/// <summary>
///     Fixed-weight heuristic scoring strategy.
///     Uses hand-tuned weights with genre similarity as the dominant signal.
///     Includes a genre-mismatch penalty to suppress items with no genre overlap.
///     This strategy does not support learning — weights are constant.
/// </summary>
public sealed class HeuristicScoringStrategy : IScoringStrategy
{
    /// <summary>Weight for genre similarity signal (dominant).</summary>
    internal const double GenreWeight = 0.50;

    /// <summary>Weight for collaborative filtering signal.</summary>
    internal const double CollaborativeWeight = 0.20;

    /// <summary>Weight for community rating signal.</summary>
    internal const double RatingWeight = 0.10;

    /// <summary>Weight for recency signal.</summary>
    internal const double RecencyWeight = 0.05;

    /// <summary>Weight for year proximity signal.</summary>
    internal const double YearProximityWeight = 0.05;

    /// <summary>
    ///     Genre similarity threshold below which the genre mismatch penalty is applied.
    /// </summary>
    internal const double GenreMismatchThreshold = 0.1;

    /// <summary>
    ///     Penalty multiplier for items below the genre mismatch threshold.
    /// </summary>
    internal const double GenreMismatchPenalty = 0.15;

    /// <inheritdoc />
    public string Name => "Heuristic (Fixed Weights)";

    /// <inheritdoc />
    public string NameKey => "strategyHeuristic";

    /// <inheritdoc />
    public double Score(CandidateFeatures features)
    {
        var score =
            (features.GenreSimilarity * GenreWeight) +
            (features.CollaborativeScore * CollaborativeWeight) +
            (features.RatingScore * RatingWeight) +
            (features.RecencyScore * RecencyWeight) +
            (features.YearProximityScore * YearProximityWeight);

        // Apply genre-mismatch penalty: items with no meaningful genre overlap
        // are strongly penalized to prevent irrelevant recommendations
        if (features.GenreSimilarity < GenreMismatchThreshold)
        {
            score *= GenreMismatchPenalty;
        }

        return score;
    }

    /// <inheritdoc />
    public bool Train(IReadOnlyList<TrainingExample> examples)
    {
        // Heuristic strategy does not support learning
        return false;
    }
}