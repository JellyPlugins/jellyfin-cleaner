using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Timeline;

/// <summary>
/// Interface for the service that computes a cumulative growth timeline
/// based on media file creation dates.
/// </summary>
public interface IGrowthTimelineService
{
    /// <summary>
    /// Computes the growth timeline by scanning top-level media directories.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The growth timeline result.</returns>
    Task<GrowthTimelineResult> ComputeTimelineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads the last computed timeline from disk.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cached timeline or null.</returns>
    Task<GrowthTimelineResult?> LoadTimelineAsync(CancellationToken cancellationToken);
}