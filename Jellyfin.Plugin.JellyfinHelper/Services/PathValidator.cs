using System;
using System.IO;

namespace Jellyfin.Plugin.JellyfinHelper.Services;

/// <summary>
/// Provides path validation utilities to prevent path traversal attacks.
/// </summary>
internal static class PathValidator
{
    /// <summary>
    /// Validates that a given path does not contain path traversal sequences
    /// and resolves to a location within the allowed base directory.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="allowedBaseDirectory">The allowed base directory.</param>
    /// <returns><c>true</c> if the path is safe; <c>false</c> otherwise.</returns>
    internal static bool IsSafePath(string? path, string allowedBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Reject obvious traversal patterns
        if (path.Contains("..", StringComparison.Ordinal) ||
            path.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var basePath = Path.GetFullPath(allowedBaseDirectory);

            // Ensure trailing separator for correct prefix matching
            if (!basePath.EndsWith(Path.DirectorySeparatorChar))
            {
                basePath += Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Sanitizes a filename by removing any directory components and invalid characters.
    /// </summary>
    /// <param name="fileName">The raw filename input.</param>
    /// <returns>A sanitized filename safe for use in file operations.</returns>
    internal static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "export";
        }

        // Strip any directory separators
        var name = Path.GetFileName(fileName);

        // Remove invalid filename characters
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "export" : name;
    }
}