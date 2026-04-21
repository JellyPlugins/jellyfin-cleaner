using System.Collections.Generic;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation;

/// <summary>
///     Fixed-weight heuristic scoring strategy.
///     Uses hand-tuned weights from <see cref="DefaultWeights"/> with genre similarity as the dominant signal.
///     This strategy does not apply genre-mismatch penalties — that responsibility
///     belongs to the ensemble layer to avoid double-penalization.
///     This strategy does not support learning — weights are constant.
/// </summary>
public sealed class HeuristicScoringStrategy : IScoringStrategy
{
    private static readonly double[] Weights = DefaultWeights.CreateWeightArray();

    /// <inheritdoc />
    public string Name => "Heuristic (Fixed Weights)";

    /// <inheritdoc />
    public string NameKey => "strategyHeuristic";

    /// <inheritdoc />
    public double Score(CandidateFeatures features)
    {
        return ScoreWithExplanation(features).FinalScore;
    }

    /// <inheritdoc />
    public ScoreExplanation ScoreWithExplanation(CandidateFeatures features)
    {
        var vector = features.ToVector();
        return ScoringHelper.BuildExplanation(vector, Weights, bias: 0.0, Name);
    }

    /// <inheritdoc />
    public bool Train(IReadOnlyList<TrainingExample> examples)
    {
        // Heuristic strategy does not support learning
        return false;
    }
}