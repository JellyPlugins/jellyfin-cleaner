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
    ///     Half-life for genre preference temporal decay in days (~180 days).
    ///     Genres watched recently contribute more than genres watched months ago.
    /// </summary>
    private const double GenreDecayHalfLifeDays = 180.0;

    /// <summary>Decay constant derived from half-life: ln(2) / halfLife.</summary>
    private static readonly double GenreDecayConstant = Math.Log(2.0) / GenreDecayHalfLifeDays;

    /// <summary>
    ///     Builds a normalized genre preference vector from the user's watch history.
    ///     Each genre gets a weight based on recency, play count, and favorites.
    ///     Recent watches count more than old ones (180-day half-life exponential decay).
    ///     Re-watched items get a PlayCount boost. Favorites get an additional boost.
    /// </summary>
    /// <param name="profile">The user's watch profile.</param>
    /// <returns>A dictionary mapping genre names to normalized weights (0–1).</returns>
    internal static Dictionary<string, double> BuildGenrePreferenceVector(UserWatchProfile profile)
    {
        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (profile.WatchedItems.Count == 0 && profile.GenreDistribution.Count == 0)
        {
            return vector;
        }

        // Build genre preferences with temporal decay — recent watches count more
        var now = DateTime.UtcNow;
        foreach (var item in profile.WatchedItems)
        {
            if (!item.Played || item.Genres is null)
            {
                continue;
            }

            // Compute temporal weight: exponential decay with ~180-day half-life
            var ageDays = item.LastPlayedDate.HasValue
                ? Math.Max(0, (now - item.LastPlayedDate.Value).TotalDays)
                : 365.0; // Default to ~1 year for items without timestamp
            var temporalWeight = Math.Exp(-GenreDecayConstant * ageDays);

            // PlayCount boost: re-watched items signal stronger preference
            var playCountBoost = Math.Min(item.PlayCount, 5) * 0.2; // max 1.0 extra from re-watches
            var weight = temporalWeight + playCountBoost;

            // Favorite boost
            if (item.IsFavorite)
            {
                weight += EngineConstants.FavoriteGenreBoostFactor;
            }

            foreach (var genre in item.Genres)
            {
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    vector.TryGetValue(genre, out var current);
                    vector[genre] = current + weight;
                }
            }
        }

        // Merge GenreDistribution as base weights for genres not covered by WatchedItems.
        // This ensures backward compatibility and catches genres from items whose
        // WatchedItemInfo has no Genres array (e.g. episodes inheriting parent series genres).
        foreach (var (genre, count) in profile.GenreDistribution)
        {
            if (!vector.ContainsKey(genre) && count > 0)
            {
                vector[genre] = count;
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