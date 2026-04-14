using System.Collections.Generic;
using System.Threading;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Strm;

/// <summary>
/// Interface for the service that finds and repairs broken .strm file references.
/// </summary>
public interface IStrmRepairService
{
    /// <summary>
    /// Scans the given library paths for .strm files, validates their target paths,
    /// and repairs broken references by searching the parent directory for a media file.
    /// </summary>
    /// <param name="libraryPaths">The library paths to scan for .strm files.</param>
    /// <param name="dryRun">If true, no files will be modified.</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
    /// <returns>The result of the repair operation.</returns>
    StrmRepairResult RepairStrmFiles(IEnumerable<string> libraryPaths, bool dryRun, CancellationToken cancellationToken = default);
}