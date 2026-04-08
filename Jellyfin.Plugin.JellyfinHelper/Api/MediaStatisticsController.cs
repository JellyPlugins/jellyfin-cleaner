using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.JellyfinHelper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Api;

/// <summary>
/// API controller for media statistics with caching, rate limiting, export, and history.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("JellyfinCleaner")]
[Produces(MediaTypeNames.Application.Json)]
public class MediaStatisticsController : ControllerBase
{
    private const string StatsCacheKey = "JellyfinHelper_Statistics";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan MinScanInterval = TimeSpan.FromSeconds(30);
    private static readonly object RateLimitLock = new();
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Simple in-memory rate limiting
    private static DateTime _lastScanTime = DateTime.MinValue;

    private readonly MediaStatisticsService _statisticsService;
    private readonly StatisticsHistoryService _historyService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MediaStatisticsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaStatisticsController"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="logger">The controller logger.</param>
    /// <param name="serviceLogger">The statistics service logger.</param>
    /// <param name="historyLogger">The history service logger.</param>
    public MediaStatisticsController(
        ILibraryManager libraryManager,
        IFileSystem fileSystem,
        IApplicationPaths applicationPaths,
        IMemoryCache cache,
        ILogger<MediaStatisticsController> logger,
        ILogger<MediaStatisticsService> serviceLogger,
        ILogger<StatisticsHistoryService> historyLogger)
    {
        _statisticsService = new MediaStatisticsService(libraryManager, fileSystem, serviceLogger);
        _historyService = new StatisticsHistoryService(applicationPaths, historyLogger);
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets media statistics for all libraries. Results are cached for 5 minutes.
    /// </summary>
    /// <param name="forceRefresh">Set to true to bypass the cache and force a fresh scan.</param>
    /// <returns>The media statistics.</returns>
    [HttpGet("Statistics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult<MediaStatisticsResult> GetStatistics([FromQuery] bool forceRefresh = false)
    {
        // Try cache first (unless force refresh)
        if (!forceRefresh && _cache.TryGetValue(StatsCacheKey, out MediaStatisticsResult? cached) && cached != null)
        {
            _logger.LogDebug("Returning cached statistics");
            return Ok(cached);
        }

        // Rate limiting: prevent excessive scans
        lock (RateLimitLock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastScanTime < MinScanInterval)
            {
                // Check cache again inside lock (another request might have populated it)
                if (_cache.TryGetValue(StatsCacheKey, out MediaStatisticsResult? recentCached) && recentCached != null)
                {
                    return Ok(recentCached);
                }

                _logger.LogWarning("Rate limit exceeded for statistics scan");
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Please wait before requesting another scan." });
            }

            _lastScanTime = now;
        }

        var result = _statisticsService.CalculateStatistics();

        // Cache the result
        _cache.Set(StatsCacheKey, result, CacheDuration);

        // Save snapshot for historical tracking
        try
        {
            _historyService.SaveSnapshot(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save statistics snapshot");
        }

        return Ok(result);
    }

    /// <summary>
    /// Exports the current statistics as a JSON file download.
    /// </summary>
    /// <returns>A JSON file containing the statistics.</returns>
    [HttpGet("Statistics/Export/Json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult ExportJson()
    {
        var result = GetCachedOrCalculate();

        var json = JsonSerializer.Serialize(result, ExportJsonOptions);

        var bytes = Encoding.UTF8.GetBytes(json);
        var timestamp = result.ScanTimestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return File(bytes, "application/json", $"jellyfin-statistics-{timestamp}.json");
    }

    /// <summary>
    /// Exports the current statistics as a CSV file download.
    /// </summary>
    /// <returns>A CSV file containing the per-library statistics.</returns>
    [HttpGet("Statistics/Export/Csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult ExportCsv()
    {
        var result = GetCachedOrCalculate();

        var sb = new StringBuilder();
        sb.AppendLine("Library,CollectionType,VideoFiles,VideoSizeBytes,AudioFiles,AudioSizeBytes,SubtitleFiles,SubtitleSizeBytes,ImageFiles,ImageSizeBytes,NfoFiles,NfoSizeBytes,TrickplayFolders,TrickplaySizeBytes,OtherFiles,OtherSizeBytes,TotalSizeBytes");

        foreach (var lib in result.Libraries)
        {
            sb.AppendLine(string.Join(
                ",",
                EscapeCsv(lib.LibraryName),
                EscapeCsv(lib.CollectionType),
                lib.VideoFileCount,
                lib.VideoSize,
                lib.AudioFileCount,
                lib.AudioSize,
                lib.SubtitleFileCount,
                lib.SubtitleSize,
                lib.ImageFileCount,
                lib.ImageSize,
                lib.NfoFileCount,
                lib.NfoSize,
                lib.TrickplayFolderCount,
                lib.TrickplaySize,
                lib.OtherFileCount,
                lib.OtherSize,
                lib.TotalSize));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var timestamp = result.ScanTimestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return File(bytes, "text/csv", $"jellyfin-statistics-{timestamp}.csv");
    }

    /// <summary>
    /// Gets the historical statistics trend data.
    /// </summary>
    /// <returns>A list of historical snapshots.</returns>
    [HttpGet("Statistics/History")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<StatisticsSnapshot>> GetHistory()
    {
        var history = _historyService.LoadHistory();
        return Ok(history);
    }

    /// <summary>
    /// Returns cached statistics or calculates fresh ones.
    /// </summary>
    private MediaStatisticsResult GetCachedOrCalculate()
    {
        if (_cache.TryGetValue(StatsCacheKey, out MediaStatisticsResult? cached) && cached != null)
        {
            return cached;
        }

        var result = _statisticsService.CalculateStatistics();
        _cache.Set(StatsCacheKey, result, CacheDuration);
        return result;
    }

    /// <summary>
    /// Escapes a value for CSV output.
    /// </summary>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}