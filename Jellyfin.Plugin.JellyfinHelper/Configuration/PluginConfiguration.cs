using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyfinHelper.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the library names to include (whitelist). Empty means all libraries are included.
    /// Comma-separated list of library names.
    /// </summary>
    public string IncludedLibraries { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the library names to exclude (blacklist).
    /// Comma-separated list of library names.
    /// </summary>
    public string ExcludedLibraries { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum age in days an orphaned item must have before it is eligible for deletion.
    /// This protects against race conditions with active downloads. Default is 0 (immediate).
    /// </summary>
    public int OrphanMinAgeDays { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether cleanup tasks should run in dry-run mode by default.
    /// When enabled, the regular cleanup tasks will only log what would be deleted without actually deleting.
    /// </summary>
    public bool DryRunByDefault { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use a trash folder instead of permanently deleting files.
    /// </summary>
    public bool UseTrash { get; set; }

    /// <summary>
    /// Gets or sets the path to the trash folder. Defaults to ".jellyfin-trash" inside the library root.
    /// </summary>
    public string TrashFolderPath { get; set; } = ".jellyfin-trash";

    /// <summary>
    /// Gets or sets the number of days to keep items in the trash before permanent deletion.
    /// Default is 30 days.
    /// </summary>
    public int TrashRetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether the orphaned subtitle cleaner is enabled.
    /// </summary>
    public bool EnableSubtitleCleaner { get; set; } = true;

    /// <summary>
    /// Gets or sets the Radarr API URL (e.g., http://localhost:7878).
    /// </summary>
    public string RadarrUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Radarr API key.
    /// </summary>
    public string RadarrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Sonarr API URL (e.g., http://localhost:8989).
    /// </summary>
    public string SonarrUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Sonarr API key.
    /// </summary>
    public string SonarrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UI language code. Default is "en".
    /// Supported: en, de, fr, es, pt, zh, tr.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Gets or sets the total bytes freed by all cleanup operations since the plugin was installed.
    /// This value is persisted and accumulated across runs.
    /// </summary>
    public long TotalBytesFreed { get; set; }

    /// <summary>
    /// Gets or sets the total number of items deleted by all cleanup operations since the plugin was installed.
    /// </summary>
    public int TotalItemsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last cleanup run.
    /// </summary>
    public DateTime LastCleanupTimestamp { get; set; } = DateTime.MinValue;
}