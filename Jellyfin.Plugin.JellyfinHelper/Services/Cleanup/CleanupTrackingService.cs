using System;
using System.Threading;
using Jellyfin.Plugin.JellyfinHelper.Services.PluginLog;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;

/// <summary>
/// Tracks cleanup statistics (bytes freed, items deleted) and persists them in the plugin configuration.
/// Thread-safe: multiple cleanup tasks may call <see cref="RecordCleanup"/> concurrently.
/// Registered as a singleton via DI; reads configuration through <see cref="ICleanupConfigHelper"/>.
/// </summary>
public class CleanupTrackingService : ICleanupTrackingService
{
    private readonly Lock _syncLock = new();
    private readonly ICleanupConfigHelper _configHelper;
    private readonly IPluginLogService _pluginLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanupTrackingService"/> class.
    /// </summary>
    /// <param name="configHelper">The cleanup configuration helper.</param>
    /// <param name="pluginLog">The plugin log service.</param>
    public CleanupTrackingService(ICleanupConfigHelper configHelper, IPluginLogService pluginLog)
    {
        _configHelper = configHelper;
        _pluginLog = pluginLog;
    }

    /// <inheritdoc />
    public void RecordCleanup(long bytesFreed, int itemsDeleted, ILogger logger)
    {
        lock (_syncLock)
        {
            var config = _configHelper.GetConfig();
            config.TotalBytesFreed += bytesFreed;
            config.TotalItemsDeleted += itemsDeleted;
            config.LastCleanupTimestamp = DateTime.UtcNow;

            Plugin.Instance?.SaveConfiguration();

            _pluginLog.LogInfo("CleanupTracking", $"Cleanup recorded: {bytesFreed} bytes freed, {itemsDeleted} items deleted. Lifetime total: {config.TotalBytesFreed} bytes, {config.TotalItemsDeleted} items.", logger);
        }
    }

    /// <inheritdoc />
    public (long TotalBytesFreed, int TotalItemsDeleted, DateTime LastCleanupTimestamp) GetStatistics()
    {
        var config = _configHelper.GetConfig();
        return (config.TotalBytesFreed, config.TotalItemsDeleted, config.LastCleanupTimestamp);
    }
}