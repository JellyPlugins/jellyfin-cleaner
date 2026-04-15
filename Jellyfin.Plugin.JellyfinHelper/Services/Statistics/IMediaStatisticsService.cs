namespace Jellyfin.Plugin.JellyfinHelper.Services.Statistics;

/// <summary>
/// Interface for the service that calculates media file statistics per library type.
/// </summary>
public interface IMediaStatisticsService
{
    /// <summary>
    /// Calculates statistics for all configured libraries.
    /// </summary>
    /// <returns>The aggregated media statistics.</returns>
    MediaStatisticsResult CalculateStatistics();
}