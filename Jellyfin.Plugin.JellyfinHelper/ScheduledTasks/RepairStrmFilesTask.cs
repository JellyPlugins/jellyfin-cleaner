using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyfinHelper.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.ScheduledTasks;

/// <summary>
/// Scheduled task that scans for broken .strm files and repairs them
/// by searching the parent directory for a renamed media file.
/// </summary>
public class RepairStrmFilesTask : IScheduledTask
{
    private readonly ILogger<RepairStrmFilesTask> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly StrmRepairService _strmRepairService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RepairStrmFilesTask"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="strmRepairServiceLogger">The logger for the strm repair service.</param>
    public RepairStrmFilesTask(
        ILogger<RepairStrmFilesTask> logger,
        ILibraryManager libraryManager,
        IFileSystem fileSystem,
        ILogger<StrmRepairService> strmRepairServiceLogger)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _strmRepairService = new StrmRepairService(fileSystem, strmRepairServiceLogger);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RepairStrmFilesTask"/> class.
    /// This constructor is used for testing to inject a mock service.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="strmRepairService">The strm repair service.</param>
    internal RepairStrmFilesTask(
        ILogger<RepairStrmFilesTask> logger,
        ILibraryManager libraryManager,
        StrmRepairService strmRepairService)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _strmRepairService = strmRepairService;
    }

    /// <inheritdoc />
    public string Name => "Repair broken .strm files";

    /// <inheritdoc />
    public string Key => "RepairStrmFiles";

    /// <inheritdoc />
    public string Description => "Scans media libraries for .strm files with broken target paths and attempts to repair them by searching the parent directory for renamed media files.";

    /// <inheritdoc />
    public string Category => "JellyfinHelper";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting .strm file repair task");
        progress.Report(0);

        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is null, cannot execute .strm repair task");
            progress.Report(100);
            return Task.CompletedTask;
        }

        if (!config.EnableStrmRepair)
        {
            _logger.LogInformation(".strm repair is disabled in plugin configuration");
            progress.Report(100);
            return Task.CompletedTask;
        }

        var libraryPaths = CleanupConfigHelper.GetFilteredLibraryLocations(_libraryManager);

        if (libraryPaths.Count == 0)
        {
            _logger.LogWarning("No library paths configured for .strm repair");
            progress.Report(100);
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Running .strm repair (DryRun: {DryRun}) on {Count} library paths: {Paths}",
            config.StrmRepairDryRun,
            libraryPaths.Count,
            string.Join(", ", libraryPaths));

        progress.Report(10);

        var result = _strmRepairService.RepairStrmFiles(libraryPaths, config.StrmRepairDryRun);

        progress.Report(90);

        _logger.LogInformation(
            ".strm repair task finished. Results: {Valid} valid, {Repaired} repaired, {Broken} broken (unfixable), {Ambiguous} ambiguous, {Invalid} invalid content",
            result.ValidCount,
            result.RepairedCount,
            result.BrokenCount,
            result.AmbiguousCount,
            result.InvalidContentCount);

        progress.Report(100);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(5).Ticks,
            },
        ];
    }
}