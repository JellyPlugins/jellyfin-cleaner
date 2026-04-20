using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Timeline;

/// <summary>
///     Contains the aggregated library insights: largest entries and recently changed/added entries.
/// </summary>
public sealed class LibraryInsightsResult
{
    /// <summary>
    ///     Gets or sets the top largest media directories sorted by size descending.
    ///     Contains up to 10 movies and 10 TV shows.
    /// </summary>
    public IReadOnlyList<LibraryInsightEntry> Largest { get; set; } = Array.Empty<LibraryInsightEntry>();

    /// <summary>
    ///     Gets or sets the combined total size of all entries in <see cref="Largest"/>.
    /// </summary>
    public long LargestTotalSize { get; set; }

    /// <summary>
    ///     Gets or sets media directories added or changed within the last 30 days,
    ///     sorted by the relevant date descending.
    /// </summary>
    public IReadOnlyList<LibraryInsightEntry> Recent { get; set; } = Array.Empty<LibraryInsightEntry>();

    /// <summary>
    ///     Gets or sets the total count of entries added or changed within the last 30 days.
    ///     This may be larger than <see cref="Recent"/> if more entries exist.
    /// </summary>
    public int RecentTotalCount { get; set; }

    /// <summary>
    ///     Gets or sets per-library size totals (library name → total bytes).
    /// </summary>
    public IReadOnlyDictionary<string, long> LibrarySizes { get; set; } = new Dictionary<string, long>();

    /// <summary>
    ///     Gets or sets when this result was computed (UTC).
    /// </summary>
    public DateTime ComputedAtUtc { get; set; }
}