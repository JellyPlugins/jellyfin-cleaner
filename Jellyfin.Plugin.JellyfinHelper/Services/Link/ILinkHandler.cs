namespace Jellyfin.Plugin.JellyfinHelper.Services.Link;

/// <summary>
///     Strategy interface for handling a specific type of link file.
///     Each implementation encapsulates the logic for one link type
///     (e.g. .strm files, symbolic links), making it easy to add or
///     remove link type support independently.
/// </summary>
public interface ILinkHandler
{
    /// <summary>
    ///     Gets a value indicating whether this link type can legitimately
    ///     contain URL targets (e.g. <c>http://</c>). When true, URL-based
    ///     targets are treated as valid and skipped during repair.
    /// </summary>
    bool SupportsUrlTargets { get; }

    /// <summary>
    ///     Determines whether this handler can process the given file path.
    ///     Implementations should be fast (e.g. extension check) where possible.
    /// </summary>
    /// <param name="filePath">The file path to evaluate.</param>
    /// <returns>True if this handler recognizes the file as its link type.</returns>
    bool CanHandle(string filePath);

    /// <summary>
    ///     Reads the target path that this link file points to.
    /// </summary>
    /// <param name="filePath">The path to the link file.</param>
    /// <returns>The target path, or null if the target cannot be read.</returns>
    string? ReadTarget(string filePath);

    /// <summary>
    ///     Writes (or recreates) the link to point to a new target path.
    /// </summary>
    /// <param name="filePath">The path to the link file.</param>
    /// <param name="targetPath">The new target path.</param>
    void WriteTarget(string filePath, string targetPath);
}