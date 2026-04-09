using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services;

/// <summary>
/// Provides reusable, robust filesystem operations with proper error handling.
/// All methods gracefully handle <see cref="IOException"/> and <see cref="UnauthorizedAccessException"/>
/// by logging a warning and continuing, ensuring that inaccessible directories never crash the caller.
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// Calculates the total size of all files in a directory tree.
    /// Inaccessible directories are logged and skipped.
    /// </summary>
    /// <param name="fileSystem">The Jellyfin file system abstraction.</param>
    /// <param name="directoryPath">The root directory path.</param>
    /// <param name="logger">The logger for warning on inaccessible paths.</param>
    /// <returns>The total size in bytes.</returns>
    public static long CalculateDirectorySize(IFileSystem fileSystem, string directoryPath, ILogger logger)
    {
        long totalSize = 0;

        try
        {
            var files = fileSystem.GetFiles(directoryPath, false);
            totalSize += files.Sum(f => f.Length);

            var subDirs = fileSystem.GetDirectories(directoryPath, false);
            foreach (var subDir in subDirs)
            {
                totalSize += CalculateDirectorySize(fileSystem, subDir.FullName, logger);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not access directory {Path}", directoryPath);
        }

        return totalSize;
    }

    /// <summary>
    /// Increments a counter in a dictionary by 1.
    /// </summary>
    /// <param name="dict">The dictionary to update.</param>
    /// <param name="key">The key to increment.</param>
    public static void IncrementCount(Dictionary<string, int> dict, string key)
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

    /// <summary>
    /// Accumulates a value in a dictionary.
    /// </summary>
    /// <param name="dict">The dictionary to update.</param>
    /// <param name="key">The key to accumulate.</param>
    /// <param name="value">The value to add.</param>
    public static void AccumulateValue(Dictionary<string, long> dict, string key, long value)
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