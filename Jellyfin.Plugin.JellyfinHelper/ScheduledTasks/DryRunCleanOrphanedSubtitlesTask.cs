using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.ScheduledTasks;

/// <summary>
/// A scheduled task to perform a dry run of the orphaned subtitle cleanup.
/// </summary>
public class DryRunCleanOrphanedSubtitlesTask : CleanOrphanedSubtitlesTask
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DryRunCleanOrphanedSubtitlesTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    public DryRunCleanOrphanedSubtitlesTask(ILibraryManager libraryManager, IFileSystem fileSystem, ILogger<DryRunCleanOrphanedSubtitlesTask> logger)
        : base(libraryManager, fileSystem, logger)
    {
    }

    /// <inheritdoc />
    public override string Name => "Orphaned Subtitle Cleaner (Dry Run)";

    /// <inheritdoc />
    public override string Key => "OrphanedSubtitleCleanerDryRun";

    /// <inheritdoc />
    public override string Description => "Logs which orphaned subtitle files would be deleted without actually deleting them.";

    /// <inheritdoc />
    public override Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        return ExecuteInternalAsync(true, progress, cancellationToken);
    }

    /// <inheritdoc />
    public override IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // No default triggers for dry run
        return Array.Empty<TaskTriggerInfo>();
    }
}