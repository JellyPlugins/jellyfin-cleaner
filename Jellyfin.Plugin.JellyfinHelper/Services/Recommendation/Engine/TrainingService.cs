using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.JellyfinHelper.Services.PluginLog;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Scoring;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.WatchHistory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Engine;

/// <summary>
///     Handles training of scoring strategies using implicit feedback
///     from previous recommendation results and current watch data.
/// </summary>
internal sealed class TrainingService
{
    private readonly IPluginLogService _pluginLog;
    private readonly ILogger _logger;
    private readonly IWatchHistoryService _watchHistoryService;

    internal TrainingService(
        IWatchHistoryService watchHistoryService,
        IPluginLogService pluginLog,
        ILogger logger)
    {
        _watchHistoryService = watchHistoryService;
        _pluginLog = pluginLog;
        _logger = logger;
    }

    /// <summary>
    ///     Trains the active scoring strategy using implicit feedback from previous recommendations.
    ///     Compares previously recommended items against current watch data.
    /// </summary>
    /// <param name="strategy">The scoring strategy to train.</param>
    /// <param name="previousResults">The recommendation results from the previous run.</param>
    /// <param name="incremental">When true, subsample older examples for efficiency.</param>
    /// <returns>True if training was performed, false if skipped.</returns>
    internal bool Train(
        IScoringStrategy strategy,
        IReadOnlyList<RecommendationResult> previousResults,
        bool incremental = false)
    {
        if (previousResults.Count == 0)
        {
            _pluginLog.LogInfo("Recommendations", "Training skipped — no previous recommendations available.", _logger);
            return false;
        }

        var allProfiles = _watchHistoryService.GetAllUserWatchProfiles();
        var profileLookup = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var profile in allProfiles)
        {
            profileLookup[profile.UserId] = new HashSet<Guid>(
                profile.WatchedItems.Where(w => w.Played).Select(w => w.ItemId));
        }

        var seriesLookup = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var profile in allProfiles)
        {
            seriesLookup[profile.UserId] = new HashSet<Guid>(
                profile.WatchedItems
                    .Where(w => w.Played && w.SeriesId.HasValue)
                    .Select(w => w.SeriesId!.Value));
        }

        var examples = new List<TrainingExample>();

        foreach (var prevResult in previousResults)
        {
            if (!profileLookup.TryGetValue(prevResult.UserId, out var watchedIds))
            {
                continue;
            }

            seriesLookup.TryGetValue(prevResult.UserId, out var watchedSeriesIds);

            var userProfile = allProfiles.FirstOrDefault(p => p.UserId == prevResult.UserId);
            if (userProfile is null)
            {
                continue;
            }

            var genrePreferences = PreferenceBuilder.BuildGenrePreferenceVector(userProfile);
            var coOccurrence = CollaborativeFilter.BuildCollaborativeMap(userProfile, allProfiles);
            var collaborativeMax = coOccurrence.Count > 0 ? coOccurrence.Values.Max() : 0;
            var avgYear = ContentScoring.ComputeAverageYear(userProfile);

            var watchedItemLookup = new Dictionary<Guid, WatchedItemInfo>(userProfile.WatchedItems.Count);
            foreach (var w in userProfile.WatchedItems)
            {
                watchedItemLookup.TryAdd(w.ItemId, w);
            }

            foreach (var rec in prevResult.Recommendations)
            {
                var wasWatched = watchedIds.Contains(rec.ItemId)
                    || (watchedSeriesIds?.Contains(rec.ItemId) ?? false);

                watchedItemLookup.TryGetValue(rec.ItemId, out var watchedItemForRec);

                var features = new CandidateFeatures
                {
                    GenreSimilarity = SimilarityComputer.ComputeGenreSimilarity(rec.Genres ?? [], genrePreferences),
                    CollaborativeScore = ContentScoring.ComputeCollaborativeScore(rec.ItemId, coOccurrence, collaborativeMax),
                    RatingScore = ContentScoring.NormalizeRating(rec.CommunityRating),
                    RecencyScore = rec.PremiereDate.HasValue
                        ? ContentScoring.ComputeRecencyScore(rec.PremiereDate.Value)
                        : 0.5,
                    YearProximityScore = ContentScoring.ComputeYearProximity(rec.Year, avgYear),
                    GenreCount = rec.Genres?.Count ?? 0,
                    IsSeries = string.Equals(rec.ItemType, "Series", StringComparison.OrdinalIgnoreCase),
                    UserRatingScore = ContentScoring.ComputeUserRatingScore(watchedItemForRec),
                    CompletionRatio = ContentScoring.ComputeCompletionRatio(watchedItemForRec)
                };

                double label;
                if (wasWatched)
                {
                    label = ContentScoring.ComputeEngagementLabel(features.CompletionRatio);
                }
                else if (features.CompletionRatio > 0 && features.CompletionRatio < EngineConstants.AbandonedCompletionThreshold)
                {
                    label = EngineConstants.AbandonedLabel;
                }
                else
                {
                    label = EngineConstants.ExposureLabel;
                }

                examples.Add(new TrainingExample
                {
                    Features = features,
                    Label = label,
                    GeneratedAtUtc = prevResult.GeneratedAt
                });
            }
        }

        var positiveCount = examples.Count(e => e.Label > 0.5);
        _pluginLog.LogInfo(
            "Recommendations",
            $"Built {examples.Count} training examples ({positiveCount} positive, " +
            $"{examples.Count - positiveCount} negative) from {previousResults.Count} users.",
            _logger);

        List<TrainingExample> trainingExamples = examples;
        if (incremental && examples.Count >= EngineConstants.IncrementalMinExamplesThreshold)
        {
            var latestGeneratedAt = previousResults.Max(r => r.GeneratedAt);
            var cutoff = latestGeneratedAt.AddDays(-1);

            var newExamples = new List<TrainingExample>();
            var oldExamples = new List<TrainingExample>();

            foreach (var ex in examples)
            {
                if (ex.GeneratedAtUtc >= cutoff)
                {
                    newExamples.Add(ex);
                }
                else
                {
                    oldExamples.Add(ex);
                }
            }

            if (oldExamples.Count > 0)
            {
                var rng = new Random(42);
                var sampleCount = Math.Max(1, (int)(oldExamples.Count * EngineConstants.IncrementalOldSampleRatio));

                for (var i = 0; i < Math.Min(sampleCount, oldExamples.Count); i++)
                {
                    var j = rng.Next(i, oldExamples.Count);
                    (oldExamples[i], oldExamples[j]) = (oldExamples[j], oldExamples[i]);
                }

                var sampledOld = oldExamples.GetRange(0, sampleCount);
                var combined = new List<TrainingExample>(newExamples.Count + sampleCount);
                combined.AddRange(newExamples);
                combined.AddRange(sampledOld);
                trainingExamples = combined;

                _pluginLog.LogInfo(
                    "Recommendations",
                    $"Incremental training: {newExamples.Count} new + {sampleCount} sampled old " +
                    $"(from {oldExamples.Count} total old) = {trainingExamples.Count} examples.",
                    _logger);
            }
            else
            {
                trainingExamples = newExamples;
            }
        }

        var trained = (strategy is ITrainableStrategy trainable) && trainable.Train(trainingExamples);

        if (trained)
        {
            _pluginLog.LogInfo(
                "Recommendations",
                $"Strategy '{strategy.Name}' training completed successfully.",
                _logger);
        }
        else
        {
            _pluginLog.LogInfo(
                "Recommendations",
                $"Strategy '{strategy.Name}' training skipped (insufficient training data).",
                _logger);
        }

        return trained;
    }
}
