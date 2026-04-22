using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.WatchHistory;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Engine;

/// <summary>
///     Collaborative filtering logic: builds co-occurrence maps from user watch overlap
///     using Jaccard similarity, and pre-computes user watch sets for performance.
/// </summary>
internal static class CollaborativeFilter
{
    /// <summary>
    ///     Pre-computes watched-item HashSets for all users at once.
    ///     Called once in batch recommendation generation and shared across all per-user calls
    ///     to avoid rebuilding O(U) HashSets per user (O(U²) total → O(U) total).
    ///     Each set includes both direct item IDs and parent series IDs from episode watches.
    /// </summary>
    /// <param name="allProfiles">All user watch profiles.</param>
    /// <returns>A dictionary mapping user ID to their combined watched-item set.</returns>
    internal static Dictionary<Guid, HashSet<Guid>> PrecomputeUserWatchSets(Collection<UserWatchProfile> allProfiles)
    {
        var result = new Dictionary<Guid, HashSet<Guid>>(allProfiles.Count);

        foreach (var profile in allProfiles)
        {
            var combined = new HashSet<Guid>();
            foreach (var w in profile.WatchedItems)
            {
                if (!w.Played)
                {
                    continue;
                }

                combined.Add(w.ItemId);
                if (w.SeriesId.HasValue)
                {
                    combined.Add(w.SeriesId.Value);
                }
            }

            result[profile.UserId] = combined;
        }

        return result;
    }

    /// <summary>
    ///     Builds a collaborative co-occurrence map: for each unwatched item,
    ///     accumulates Jaccard-weighted similarity from OTHER users who share watch
    ///     overlap with this user. Uses true Jaccard similarity (0–1) instead of
    ///     discretized integer weights for better precision.
    ///     When <paramref name="precomputedUserSets"/> is provided (batch mode),
    ///     uses those sets directly instead of rebuilding them per call — reducing
    ///     total complexity from O(U²×M) to O(U×M).
    /// </summary>
    /// <param name="userProfile">The target user's watch profile.</param>
    /// <param name="allProfiles">All user watch profiles.</param>
    /// <param name="precomputedUserSets">
    ///     Optional pre-computed watch sets from <see cref="PrecomputeUserWatchSets"/>.
    ///     When null, sets are computed on-the-fly (single-user mode).
    /// </param>
    /// <returns>A dictionary mapping item IDs to accumulated Jaccard-weighted scores.</returns>
    internal static Dictionary<Guid, double> BuildCollaborativeMap(
        UserWatchProfile userProfile,
        Collection<UserWatchProfile> allProfiles,
        Dictionary<Guid, HashSet<Guid>>? precomputedUserSets = null)
    {
        var coOccurrence = new Dictionary<Guid, double>();

        // Resolve the current user's combined watch set
        HashSet<Guid> userCombinedIds;
        if (precomputedUserSets is not null && precomputedUserSets.TryGetValue(userProfile.UserId, out var precomputed))
        {
            userCombinedIds = precomputed;
        }
        else
        {
            // Fallback: build on-the-fly (single-user mode)
            var userWatchedIds = new HashSet<Guid>(
                userProfile.WatchedItems.Where(w => w.Played).Select(w => w.ItemId));

            var userWatchedSeriesIds = new HashSet<Guid>(
                userProfile.WatchedItems
                    .Where(w => w.Played && w.SeriesId.HasValue)
                    .Select(w => w.SeriesId!.Value));

            userCombinedIds = new HashSet<Guid>(userWatchedIds);
            userCombinedIds.UnionWith(userWatchedSeriesIds);
        }

        if (userCombinedIds.Count == 0)
        {
            return coOccurrence;
        }

        // Iterate over all other users and compute Jaccard-weighted co-occurrence
        foreach (var otherProfile in allProfiles)
        {
            if (otherProfile.UserId == userProfile.UserId)
            {
                continue;
            }

            // Resolve the other user's combined watch set
            HashSet<Guid> otherCombinedIds;
            if (precomputedUserSets is not null && precomputedUserSets.TryGetValue(otherProfile.UserId, out var otherPrecomputed))
            {
                otherCombinedIds = otherPrecomputed;
            }
            else
            {
                // Fallback: build on-the-fly
                otherCombinedIds = new HashSet<Guid>(
                    otherProfile.WatchedItems.Where(w => w.Played).Select(w => w.ItemId));

                foreach (var w in otherProfile.WatchedItems)
                {
                    if (w.Played && w.SeriesId.HasValue)
                    {
                        otherCombinedIds.Add(w.SeriesId.Value);
                    }
                }
            }

            if (otherCombinedIds.Count == 0)
            {
                continue;
            }

            // Compute overlap count
            var overlap = 0;
            foreach (var id in userCombinedIds)
            {
                if (otherCombinedIds.Contains(id))
                {
                    overlap++;
                }
            }

            if (overlap < EngineConstants.MinCollaborativeOverlap)
            {
                continue;
            }

            // Jaccard similarity: |A ∩ B| / |A ∪ B|
            var union = userCombinedIds.Count + otherCombinedIds.Count - overlap;
            var jaccardWeight = union > 0 ? (double)overlap / union : 0.0;

            // Accumulate Jaccard-weighted co-occurrence for items the other user watched but we haven't.
            // This includes both episode IDs AND series IDs, so series candidates get collaborative scores.
            foreach (var itemId in otherCombinedIds)
            {
                if (!userCombinedIds.Contains(itemId))
                {
                    coOccurrence.TryGetValue(itemId, out var current);
                    coOccurrence[itemId] = current + jaccardWeight;
                }
            }
        }

        return coOccurrence;
    }
}