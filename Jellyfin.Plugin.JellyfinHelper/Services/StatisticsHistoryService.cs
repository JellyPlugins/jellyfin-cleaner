using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services;

/// <summary>
/// Persists statistics snapshots to a JSON file for historical trend tracking.
/// </summary>
public class StatisticsHistoryService
{
    private const string HistoryFileName = "jellyfin-helper-statistics-history.json";
    private const int MaxSnapshots = 365;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _historyFilePath;
    private readonly ILogger<StatisticsHistoryService> _logger;
    private readonly object _fileLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StatisticsHistoryService"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="logger">The logger.</param>
    public StatisticsHistoryService(IApplicationPaths applicationPaths, ILogger<StatisticsHistoryService> logger)
    {
        _logger = logger;
        _historyFilePath = Path.Combine(applicationPaths.DataPath, HistoryFileName);
    }

    /// <summary>
    /// Loads all historical snapshots from disk.
    /// </summary>
    /// <returns>A read-only list of snapshots ordered by timestamp ascending.</returns>
    public IReadOnlyList<StatisticsSnapshot> LoadHistory()
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    return Array.Empty<StatisticsSnapshot>();
                }

                var json = File.ReadAllText(_historyFilePath);
                var snapshots = JsonSerializer.Deserialize<List<StatisticsSnapshot>>(json, JsonOptions);
                return snapshots?.AsReadOnly() ?? (IReadOnlyList<StatisticsSnapshot>)Array.Empty<StatisticsSnapshot>();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not load statistics history from {Path}", _historyFilePath);
                return Array.Empty<StatisticsSnapshot>();
            }
        }
    }

    /// <summary>
    /// Appends a snapshot derived from the given result to the history file.
    /// Automatically trims old entries beyond <see cref="MaxSnapshots"/>.
    /// </summary>
    /// <param name="result">The statistics result to snapshot.</param>
    public void SaveSnapshot(MediaStatisticsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var snapshot = StatisticsSnapshot.FromResult(result);

        lock (_fileLock)
        {
            try
            {
                var history = LoadHistoryUnsafe();
                history.Add(snapshot);

                // Trim to keep only the most recent snapshots
                if (history.Count > MaxSnapshots)
                {
                    history.RemoveRange(0, history.Count - MaxSnapshots);
                }

                var directory = Path.GetDirectoryName(_historyFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(history, JsonOptions);
                File.WriteAllText(_historyFilePath, json);

                _logger.LogInformation("Saved statistics snapshot ({Count} total entries)", history.Count);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not save statistics history to {Path}", _historyFilePath);
            }
        }
    }

    /// <summary>
    /// Internal load without locking (caller must hold the lock).
    /// </summary>
    private List<StatisticsSnapshot> LoadHistoryUnsafe()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
            {
                return new List<StatisticsSnapshot>();
            }

            var json = File.ReadAllText(_historyFilePath);
            var snapshots = JsonSerializer.Deserialize<List<StatisticsSnapshot>>(json, JsonOptions);
            return snapshots ?? new List<StatisticsSnapshot>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not load statistics history from {Path}", _historyFilePath);
            return new List<StatisticsSnapshot>();
        }
    }
}