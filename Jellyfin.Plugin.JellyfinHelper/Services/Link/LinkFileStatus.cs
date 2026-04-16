namespace Jellyfin.Plugin.JellyfinHelper.Services.Link;

/// <summary>
/// Status of a link file inspection.
/// </summary>
public enum LinkFileStatus
{
    /// <summary>
    /// The target path in the link file is valid.
    /// </summary>
    Valid,

    /// <summary>
    /// The target path was broken and has been repaired (or would be in dry-run mode).
    /// </summary>
    Repaired,

    /// <summary>
    /// The target path is broken but no replacement could be found.
    /// </summary>
    Broken,

    /// <summary>
    /// The target path is broken and multiple candidates were found (ambiguous).
    /// </summary>
    Ambiguous,

    /// <summary>
    /// The link file is empty or contains invalid content.
    /// </summary>
    InvalidContent,
}