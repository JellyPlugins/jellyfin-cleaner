using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Timeline;

/// <summary>
///     Interface for the service that computes library size and recency insights
///     (largest directories and recently added/changed media).
/// </summary>
public interface ILibraryInsightsService
{
    /// <summary>
    ///     Computes library insights by scanning top-level media directories.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The library insights result.</returns>
    Task<LibraryInsightsResult> ComputeInsightsAsync(CancellationToken cancellationToken);
}