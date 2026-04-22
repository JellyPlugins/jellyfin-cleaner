using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.WatchHistory;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Engine;

/// <summary>
///     Builds user preference vectors and sets from watch history:
///     genre preferences, studio preferences, tag preferences, and people preferences.
/// </summary>
internal static class PreferenceBuilder
{
    /// <summary>
    ///     Builds a normalized genre preference vector from the user's watch history.
    ///     Each genre gets a weight based on how often the user has watched items of that genre.
    ///     Genres from favorited items receive an additional boost to better reflect true preferences.
    /// </summary>
    /// <param name="profile">The user's watch profile.</param>
    /// <returns>A dictionary mapping genre names to normalized weights (0–1).</returns>
    internal static Dictionary<string, double> BuildGenrePreferenceVector(UserWatchProfile profile)
    {
        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (profile.GenreDistribution.Count == 0)
        {
            return vector;
        }

        // Start with the base genre distribution
        foreach (var (genre, count) in profile.GenreDistribution)
        {
            vector[genre] = count;
        }

        // Boost genres from favorited items — favorites signal strong preference
        foreach (var item in profile.WatchedItems)
        {
            if (!item.IsFavorite || item.Genres is null)
            {
                continue;
            }

            foreach (var genre in item.Genres)
            {
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    vector.TryGetValue(genre, out var current);
                    vector[genre] = current + EngineConstants.FavoriteGenreBoostFactor;
                }
            }
        }

        // Normalize to 0–1 range
        if (vector.Count == 0)
        {
            return vector;
        }

        var maxWeight = vector.Values.Max();
        if (maxWeight <= 0)
        {
            return vector;
        }

        foreach (var genre in vector.Keys.ToList())
        {
            vector[genre] /= maxWeight;
        }

        return vector;
    }

    /// <summary>
    ///     Builds a set of studio names the user prefers, derived from their watched items.
    ///     Looks up the actual BaseItem objects from the candidate lookup to access Studios metadata.
    /// </summary>
    /// <param name="userProfile">The user's watch profile.</param>
    /// <param name="candidateLookup">Pre-built candidate lookup by item ID (shared across calls for performance).</param>
    /// <returns>A HashSet of preferred studio names (case-insensitive).</returns>
    internal static HashSet<string> BuildStudioPreferenceSet(
        UserWatchProfile userProfile,
        Dictionary<Guid, BaseItem> candidateLookup)
    {
        var studios = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Collect studios from watched movies and series
        foreach (var w in userProfile.WatchedItems)
        {
            if (!w.Played)
            {
                continue;
            }

            // Try direct item match (movies)
            if (candidateLookup.TryGetValue(w.ItemId, out var item) && item.Studios is { Length: > 0 })
            {
                foreach (var s in item.Studios)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        studios.Add(s);
                    }
                }
            }

            // Also try series match (episodes → parent series)
            if (w.SeriesId.HasValue && candidateLookup.TryGetValue(w.SeriesId.Value, out var seriesItem)
                && seriesItem.Studios is { Length: > 0 })
            {
                foreach (var s in seriesItem.Studios)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        studios.Add(s);
                    }
                }
            }
        }

        return studios;
    }

    /// <summary>
    ///     Builds a set of tags the user prefers, derived from their watched items.
    ///     Looks up the actual BaseItem objects from the candidate lookup to access Tags metadata.
    ///     Used for tag-based content similarity scoring.
    /// </summary>
    /// <param name="userProfile">The user's watch profile.</param>
    /// <param name="candidateLookup">Pre-built candidate lookup by item ID (shared across calls for performance).</param>
    /// <returns>A HashSet of preferred tag names (case-insensitive).</returns>
    internal static HashSet<string> BuildTagPreferenceSet(
        UserWatchProfile userProfile,
        Dictionary<Guid, BaseItem> candidateLookup)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var w in userProfile.WatchedItems)
        {
            if (!w.Played)
            {
                continue;
            }

            // Direct item match (movies)
            if (candidateLookup.TryGetValue(w.ItemId, out var item) && item.Tags is { Length: > 0 })
            {
                foreach (var t in item.Tags)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        tags.Add(t);
                    }
                }
            }

            // Series match (episodes → parent series)
            if (w.SeriesId.HasValue && candidateLookup.TryGetValue(w.SeriesId.Value, out var seriesItem)
                && seriesItem.Tags is { Length: > 0 })
            {
                foreach (var t in seriesItem.Tags)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        tags.Add(t);
                    }
                }
            }
        }

        return tags;
    }

    /// <summary>
    ///     Builds a set of preferred person names (actors/directors) from the user's watched items.
    ///     Uses the pre-built people lookup to avoid additional library queries.
    ///     Includes people from both directly watched items and series the user has watched episodes of.
    /// </summary>
    /// <param name="userProfile">The user's watch profile.</param>
    /// <param name="peopleLookup">Pre-built candidate people lookup (item ID → person names).</param>
    /// <returns>A HashSet of preferred person names (case-insensitive).</returns>
    internal static HashSet<string> BuildPeoplePreferenceSet(
        UserWatchProfile userProfile,
        Dictionary<Guid, HashSet<string>> peopleLookup)
    {
        var people = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var w in userProfile.WatchedItems)
        {
            if (!w.Played)
            {
                continue;
            }

            // Direct item match (movies, episodes)
            if (peopleLookup.TryGetValue(w.ItemId, out var itemPeople))
            {
                people.UnionWith(itemPeople);
            }

            // Series match (episodes → parent series)
            if (w.SeriesId.HasValue && peopleLookup.TryGetValue(w.SeriesId.Value, out var seriesPeople))
            {
                people.UnionWith(seriesPeople);
            }
        }

        return people;
    }
}