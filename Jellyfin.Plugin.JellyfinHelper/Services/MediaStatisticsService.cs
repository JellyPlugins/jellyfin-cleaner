using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services;

/// <summary>
/// Service that calculates media file statistics per library type.
/// </summary>
public partial class MediaStatisticsService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<MediaStatisticsService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaStatisticsService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    public MediaStatisticsService(ILibraryManager libraryManager, IFileSystem fileSystem, ILogger<MediaStatisticsService> logger)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Calculates statistics for all configured libraries.
    /// </summary>
    /// <returns>The aggregated media statistics.</returns>
    public MediaStatisticsResult CalculateStatistics()
    {
        var result = new MediaStatisticsResult();

        var virtualFolders = _libraryManager.GetVirtualFolders();

        foreach (var vf in virtualFolders)
        {
            var collectionType = vf.CollectionType;
            var isMovies = collectionType is CollectionTypeOptions.movies
                or CollectionTypeOptions.homevideos
                or CollectionTypeOptions.musicvideos;
            var isTvShows = collectionType is CollectionTypeOptions.tvshows;
            var isMusic = collectionType is CollectionTypeOptions.music;

            var libraryStats = new LibraryStatistics
            {
                LibraryName = vf.Name ?? "Unknown",
                CollectionType = collectionType?.ToString() ?? "mixed"
            };

            foreach (var location in vf.Locations)
            {
                _logger.LogDebug("Scanning library location: {Location} (type: {Type})", location, collectionType);
                AnalyzeDirectoryRecursive(location, libraryStats);
            }

            result.Libraries.Add(libraryStats);

            if (isTvShows)
            {
                result.TvShows.Add(libraryStats);
            }
            else if (isMusic)
            {
                result.Music.Add(libraryStats);
            }
            else if (isMovies)
            {
                result.Movies.Add(libraryStats);
            }
            else
            {
                result.Other.Add(libraryStats);
            }
        }

        return result;
    }

    /// <summary>
    /// Recursively analyzes a directory and accumulates file size statistics.
    /// </summary>
    /// <param name="directoryPath">The directory to analyze.</param>
    /// <param name="stats">The statistics accumulator.</param>
    private void AnalyzeDirectoryRecursive(string directoryPath, LibraryStatistics stats)
    {
        try
        {
            var files = _fileSystem.GetFiles(directoryPath, false).ToList();

            bool hasVideo = false;
            bool hasSubs = false;
            bool hasImage = false;
            bool hasNfo = false;
            bool hasAnyNonTrickplayFile = false;

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FullName);
                var size = file.Length;
                hasAnyNonTrickplayFile = true;

                if (MediaExtensions.VideoExtensions.Contains(ext))
                {
                    stats.VideoSize += size;
                    stats.VideoFileCount++;
                    hasVideo = true;

                    // Container format tracking
                    var container = ext.TrimStart('.').ToUpperInvariant();
                    IncrementDictionary(stats.ContainerFormats, container);
                    IncrementDictionaryLong(stats.ContainerSizes, container, size);

                    // Resolution parsing from filename
                    var resolution = ParseResolution(file.Name);
                    IncrementDictionary(stats.Resolutions, resolution);
                    IncrementDictionaryLong(stats.ResolutionSizes, resolution, size);

                    // Codec parsing from filename
                    var codec = ParseCodec(file.Name);
                    IncrementDictionary(stats.VideoCodecs, codec);
                    IncrementDictionaryLong(stats.VideoCodecSizes, codec, size);
                }
                else if (MediaExtensions.SubtitleExtensions.Contains(ext))
                {
                    stats.SubtitleSize += size;
                    stats.SubtitleFileCount++;
                    hasSubs = true;
                }
                else if (MediaExtensions.ImageExtensions.Contains(ext))
                {
                    stats.ImageSize += size;
                    stats.ImageFileCount++;
                    hasImage = true;
                }
                else if (MediaExtensions.NfoExtensions.Contains(ext))
                {
                    stats.NfoSize += size;
                    stats.NfoFileCount++;
                    hasNfo = true;
                }
                else if (MediaExtensions.AudioExtensions.Contains(ext))
                {
                    stats.AudioSize += size;
                    stats.AudioFileCount++;
                }
                else
                {
                    stats.OtherSize += size;
                    stats.OtherFileCount++;
                }
            }

            // Health checks — per-directory analysis
            if (hasVideo)
            {
                // Count how many video files in this directory lack companion files
                int videoCount = files.Count(f => MediaExtensions.VideoExtensions.Contains(Path.GetExtension(f.FullName)));
                if (!hasSubs)
                {
                    stats.VideosWithoutSubtitles += videoCount;
                }

                if (!hasImage)
                {
                    stats.VideosWithoutImages += videoCount;
                }

                if (!hasNfo)
                {
                    stats.VideosWithoutNfo += videoCount;
                }
            }
            else if (hasAnyNonTrickplayFile && (hasSubs || hasImage || hasNfo))
            {
                // Directory has metadata-type files but no video — orphaned metadata
                stats.OrphanedMetadataDirectories++;
            }

            // Recurse into subdirectories
            var subDirs = _fileSystem.GetDirectories(directoryPath, false);
            foreach (var subDir in subDirs)
            {
                if (subDir.Name.EndsWith(".trickplay", StringComparison.OrdinalIgnoreCase))
                {
                    var trickplaySize = CalculateDirectorySize(subDir.FullName);
                    stats.TrickplaySize += trickplaySize;
                    stats.TrickplayFolderCount++;
                }
                else
                {
                    AnalyzeDirectoryRecursive(subDir.FullName, stats);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not access directory {Path}", directoryPath);
        }
    }

    /// <summary>
    /// Parses a resolution tier from a video filename.
    /// </summary>
    /// <param name="fileName">The video filename.</param>
    /// <returns>A resolution label such as "4K", "1080p", "720p", "480p", or "Unknown".</returns>
    internal static string ParseResolution(string fileName)
    {
        if (ResolutionRegex4K().IsMatch(fileName))
        {
            return "4K";
        }

        if (ResolutionRegex1080().IsMatch(fileName))
        {
            return "1080p";
        }

        if (ResolutionRegex720().IsMatch(fileName))
        {
            return "720p";
        }

        if (ResolutionRegex480().IsMatch(fileName))
        {
            return "480p";
        }

        if (ResolutionRegex576().IsMatch(fileName))
        {
            return "576p";
        }

        return "Unknown";
    }

    /// <summary>
    /// Parses a video codec from a video filename.
    /// </summary>
    /// <param name="fileName">The video filename.</param>
    /// <returns>A codec label such as "HEVC", "H.264", "AV1", "VP9", "MPEG", or "Unknown".</returns>
    internal static string ParseCodec(string fileName)
    {
        if (CodecRegexHevc().IsMatch(fileName))
        {
            return "HEVC";
        }

        if (CodecRegexH264().IsMatch(fileName))
        {
            return "H.264";
        }

        if (CodecRegexAv1().IsMatch(fileName))
        {
            return "AV1";
        }

        if (CodecRegexVp9().IsMatch(fileName))
        {
            return "VP9";
        }

        if (CodecRegexMpeg().IsMatch(fileName))
        {
            return "MPEG";
        }

        if (CodecRegexXvid().IsMatch(fileName))
        {
            return "XviD";
        }

        if (CodecRegexDivx().IsMatch(fileName))
        {
            return "DivX";
        }

        return "Unknown";
    }

    // Source-generated regex patterns for resolution detection
    [GeneratedRegex(@"(?i)[\.\-_ \[\(](2160p|4k|uhd)[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex ResolutionRegex4K();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(]1080[pi][\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex ResolutionRegex1080();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(]720p[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex ResolutionRegex720();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(](480p|sd)[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex ResolutionRegex480();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(]576p[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex ResolutionRegex576();

    // Source-generated regex patterns for codec detection
    [GeneratedRegex(@"(?i)[\.\-_ \[\(](hevc|h\.?265|x\.?265)[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex CodecRegexHevc();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(](h\.?264|x\.?264|avc)[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex CodecRegexH264();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(]av1[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex CodecRegexAv1();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(]vp9[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex CodecRegexVp9();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(](mpeg[24]?|mp2v)[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex CodecRegexMpeg();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(]xvid[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex CodecRegexXvid();

    [GeneratedRegex(@"(?i)[\.\-_ \[\(]divx[\.\-_ \]\)]", RegexOptions.None)]
    private static partial Regex CodecRegexDivx();

    /// <summary>
    /// Calculates the total size of all files in a directory tree.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <returns>The total size in bytes.</returns>
    private long CalculateDirectorySize(string directoryPath)
    {
        long totalSize = 0;

        try
        {
            var files = _fileSystem.GetFiles(directoryPath, false);
            totalSize += files.Sum(f => f.Length);

            var subDirs = _fileSystem.GetDirectories(directoryPath, false);
            foreach (var subDir in subDirs)
            {
                totalSize += CalculateDirectorySize(subDir.FullName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not access directory {Path}", directoryPath);
        }

        return totalSize;
    }

    private static void IncrementDictionary(Dictionary<string, int> dict, string key)
    {
        if (dict.TryGetValue(key, out var current))
        {
            dict[key] = current + 1;
        }
        else
        {
            dict[key] = 1;
        }
    }

    private static void IncrementDictionaryLong(Dictionary<string, long> dict, string key, long value)
    {
        if (dict.TryGetValue(key, out var current))
        {
            dict[key] = current + value;
        }
        else
        {
            dict[key] = value;
        }
    }
}