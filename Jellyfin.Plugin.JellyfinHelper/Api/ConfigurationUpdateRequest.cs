using System.Collections.Generic;
using Jellyfin.Plugin.JellyfinHelper.Configuration;

namespace Jellyfin.Plugin.JellyfinHelper.Api;

/// <summary>
///     Request DTO for updating the plugin configuration via the API.
///     Uses arrays for Arr instances to avoid CA2227 while supporting JSON deserialization.
/// </summary>
public class ConfigurationUpdateRequest
{
    /// <summary>
    ///     Gets the library names to include (whitelist). Comma-separated.
    /// </summary>
    public string IncludedLibraries { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the library names to exclude (blacklist). Comma-separated.
    /// </summary>
    public string ExcludedLibraries { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the minimum age in days an orphaned item must have before deletion.
    /// </summary>
    public int OrphanMinAgeDays { get; init; }

    /// <summary>
    ///     Gets the execution mode for the Trickplay Folder Cleaner task.
    /// </summary>
    public TaskMode TrickplayTaskMode { get; init; } = TaskMode.DryRun;

    /// <summary>
    ///     Gets the execution mode for the Empty Media Folder Cleaner task.
    /// </summary>
    public TaskMode EmptyMediaFolderTaskMode { get; init; } = TaskMode.DryRun;

    /// <summary>
    ///     Gets the execution mode for the Orphaned Subtitle Cleaner task.
    /// </summary>
    public TaskMode OrphanedSubtitleTaskMode { get; init; } = TaskMode.DryRun;

    /// <summary>
    ///     Gets the execution mode for the .strm File Repair task.
    /// </summary>
    public TaskMode StrmRepairTaskMode { get; init; } = TaskMode.DryRun;

    /// <summary>
    ///     Gets a value indicating whether to use a trash folder instead of permanently deleting files.
    /// </summary>
    public bool UseTrash { get; init; }

    /// <summary>
    ///     Gets the path to the trash folder.
    /// </summary>
    public string TrashFolderPath { get; init; } = ".jellyfin-trash";

    /// <summary>
    ///     Gets the number of days to keep items in the trash before permanent deletion.
    /// </summary>
    public int TrashRetentionDays { get; init; } = 30;

    /// <summary>
    ///     Gets the Radarr instances (max 3).
    /// </summary>
    public IReadOnlyList<ArrInstanceConfig> RadarrInstances { get; init; } = [];

    /// <summary>
    ///     Gets the Sonarr instances (max 3).
    /// </summary>
    public IReadOnlyList<ArrInstanceConfig> SonarrInstances { get; init; } = [];

    /// <summary>
    ///     Gets the UI language code.
    /// </summary>
    public string Language { get; init; } = "en";

    /// <summary>
    ///     Gets the plugin log level (e.g. DEBUG, INFO, WARN, ERROR).
    /// </summary>
    public string PluginLogLevel { get; init; } = "INFO";
}