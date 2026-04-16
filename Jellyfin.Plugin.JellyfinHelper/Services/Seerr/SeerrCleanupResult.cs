namespace Jellyfin.Plugin.JellyfinHelper.Services.Seerr;

/// <summary>
///     Result of a Seerr cleanup operation, containing statistics about checked, expired, and deleted requests.
/// </summary>
public sealed class SeerrCleanupResult
{
    /// <summary>
    ///     Gets or sets the total number of requests checked (all approved requests retrieved from Seerr).
    /// </summary>
    public int TotalChecked { get; set; }

    /// <summary>
    ///     Gets or sets the number of requests that exceeded the maximum age threshold.
    /// </summary>
    public int ExpiredFound { get; set; }

    /// <summary>
    ///     Gets or sets the number of requests that were actually deleted (0 in dry-run mode).
    /// </summary>
    public int Deleted { get; set; }

    /// <summary>
    ///     Gets or sets the number of delete operations that failed.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the operation was a dry run.
    /// </summary>
    public bool DryRun { get; set; }
}