namespace Jellyfin.Plugin.JellyfinHelper.Services.Link;

/// <summary>
///     Abstraction for symbolic link operations to enable testing
///     without requiring real filesystem symlinks.
/// </summary>
public interface ISymlinkHelper
{
    /// <summary>
    ///     Determines whether the given path is a symbolic link.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the path is a symbolic link; otherwise, false.</returns>
    bool IsSymlink(string path);

    /// <summary>
    ///     Gets the target path of a symbolic link.
    /// </summary>
    /// <param name="path">The symbolic link path.</param>
    /// <returns>The target path, or null if not a symlink or the target cannot be read.</returns>
    string? GetSymlinkTarget(string path);

    /// <summary>
    ///     Creates a symbolic link at the specified path pointing to the given target.
    /// </summary>
    /// <param name="linkPath">The path where the symlink should be created.</param>
    /// <param name="targetPath">The target path the symlink should point to.</param>
    void CreateSymlink(string linkPath, string targetPath);

    /// <summary>
    ///     Deletes a symbolic link (without following it to the target).
    /// </summary>
    /// <param name="linkPath">The symlink path to delete.</param>
    void DeleteSymlink(string linkPath);
}