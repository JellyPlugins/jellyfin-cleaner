namespace Jellyfin.Plugin.JellyfinHelper.Services.Statistics;

/// <summary>
/// Interface for the service that caches the latest full scan result to disk.
/// </summary>
public interface IStatisticsCacheService
{
    /// <summary>
    /// Saves the latest full statistics result to disk for persistence across server restarts.
    /// </summary>
    /// <param name="result">The statistics result to persist.</param>
    void SaveLatestResult(MediaStatisticsResult result);

    /// <summary>
    /// Loads the latest full statistics result from disk.
    /// </summary>
    /// <returns>The last saved statistics result, or null if none exists.</returns>
    MediaStatisticsResult? LoadLatestResult();
}