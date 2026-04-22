using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Scoring;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.WatchHistory;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Engine;

/// <summary>
///     Determines human-readable recommendation reasons from score explanations,
///     and provides utility methods for response preparation.
/// </summary>
internal static class ReasonResolver
{
    /// <summary>
    ///     Determines the most relevant human-readable reason for a recommendation
    ///     based on the dominant signal from the score explanation.
    /// </summary>
    /// <param name="candidate">The candidate item.</param>
    /// <param name="explanation">The score explanation from the strategy.</param>
    /// <param name="genrePreferences">The user's genre preference vector.</param>
    /// <returns>A tuple of reason text, i18n key, and optional related item name.</returns>
    internal static (string Reason, string ReasonKey, string? RelatedItem) DetermineReason(
        BaseItem candidate,
        ScoreExplanation explanation,
        Dictionary<string, double> genrePreferences)
    {
        var dominant = explanation.DominantSignal;

        if (string.Equals(dominant, "Collaborative", StringComparison.OrdinalIgnoreCase)
            && explanation.CollaborativeContribution > EngineConstants.ReasonScoreThreshold)
        {
            return ("Popular with similar viewers", "reasonCollaborative", null);
        }

        if (string.Equals(dominant, "Genre", StringComparison.OrdinalIgnoreCase)
            && explanation.GenreContribution > EngineConstants.ReasonScoreThreshold
            && candidate.Genres is { Length: > 0 })
        {
            var topGenre = candidate.Genres
                .Where(g => genrePreferences.ContainsKey(g))
                .OrderByDescending(g => genrePreferences.GetValueOrDefault(g, 0))
                .FirstOrDefault();

            if (topGenre is not null)
            {
                return ($"Because you enjoy {topGenre}", "reasonGenre", topGenre);
            }
        }

        if (string.Equals(dominant, "Rating", StringComparison.OrdinalIgnoreCase)
            && explanation.RatingContribution > EngineConstants.HighRatingThreshold)
        {
            return ("Highly rated", "reasonHighlyRated", null);
        }

        if (string.Equals(dominant, "UserRating", StringComparison.OrdinalIgnoreCase))
        {
            return ("Matches your personal ratings", "reasonUserRating", null);
        }

        if (string.Equals(dominant, "Recency", StringComparison.OrdinalIgnoreCase))
        {
            return ("Recently released", "reasonRecent", null);
        }

        if (string.Equals(dominant, "Interaction", StringComparison.OrdinalIgnoreCase)
            && explanation.InteractionContribution > EngineConstants.ReasonScoreThreshold)
        {
            return ("Matches your viewing patterns", "reasonInteraction", null);
        }

        if (string.Equals(dominant, "People", StringComparison.OrdinalIgnoreCase))
        {
            return ("Features actors/directors you enjoy", "reasonPeople", null);
        }

        if (string.Equals(dominant, "Studio", StringComparison.OrdinalIgnoreCase))
        {
            return ("From a studio you enjoy", "reasonStudio", null);
        }

        return ("Recommended for you", "reasonDefault", null);
    }

    /// <summary>
    ///     Creates a copy of the profile without the full watched items list (for the API response),
    ///     keeping only aggregated stats.
    /// </summary>
    /// <param name="profile">The original user watch profile.</param>
    /// <returns>A copy of the profile with an empty watched items list.</returns>
    internal static UserWatchProfile StripWatchedItemsForResponse(UserWatchProfile profile)
    {
        return new UserWatchProfile
        {
            UserId = profile.UserId,
            UserName = profile.UserName,
            WatchedMovieCount = profile.WatchedMovieCount,
            WatchedEpisodeCount = profile.WatchedEpisodeCount,
            WatchedSeriesCount = profile.WatchedSeriesCount,
            TotalWatchTimeTicks = profile.TotalWatchTimeTicks,
            LastActivityDate = profile.LastActivityDate,
            GenreDistribution = new Dictionary<string, int>(profile.GenreDistribution),
            FavoriteCount = profile.FavoriteCount,
            AverageCommunityRating = profile.AverageCommunityRating,
            WatchedItems = []
        };
    }
}