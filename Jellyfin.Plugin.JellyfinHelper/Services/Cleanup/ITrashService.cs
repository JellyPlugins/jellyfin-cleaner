using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;

/// <summary>
/// Manages a trash/recycle bin for deleted media items instead of permanent deletion.
/// Items are moved to a timestamped trash folder and can be permanently purged after a retention period.
/// </summary>
public interface ITrashService
{
    /// <summary>
    /// Moves a directory to the trash folder instead of permanently deleting it.
    /// </summary>
    /// <param name="sourcePath">The full path of the directory to trash.</param>
    /// <param name="trashBasePath">The base path of the trash folder.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The total size in bytes of the trashed directory, or 0 if the operation failed.</returns>
    long MoveToTrash(string sourcePath, string trashBasePath, ILogger logger);

    /// <summary>
    /// Moves a single file to the trash folder instead of permanently deleting it.
    /// </summary>
    /// <param name="sourceFilePath">The full path of the file to trash.</param>
    /// <param name="trashBasePath">The base path of the trash folder.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The size in bytes of the trashed file, or 0 if the operation failed.</returns>
    long MoveFileToTrash(string sourceFilePath, string trashBasePath, ILogger logger);

    /// <summary>
    /// Purges items from the trash folder that are older than the specified retention period.
    /// </summary>
    /// <param name="trashBasePath">The base path of the trash folder.</param>
    /// <param name="retentionDays">The number of days to retain items in the trash.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The total bytes freed and items purged.</returns>
    (long BytesFreed, int ItemsPurged) PurgeExpiredTrash(string trashBasePath, int retentionDays, ILogger logger);

    /// <summary>
    /// Gets a summary of the current trash contents.
    /// </summary>
    /// <param name="trashBasePath">The base path of the trash folder.</param>
    /// <returns>A tuple of total size in bytes and item count, or (0, 0) if the trash does not exist.</returns>
    (long TotalSize, int ItemCount) GetTrashSummary(string trashBasePath);

    /// <summary>
    /// Gets detailed contents of the trash folder, including item name, size, trashed date, and purge date.
    /// </summary>
    /// <param name="trashBasePath">The base path of the trash folder.</param>
    /// <param name="retentionDays">The configured retention days to calculate purge dates.</param>
    /// <returns>A list of trash item details.</returns>
    IReadOnlyList<TrashItemInfo> GetTrashContents(string trashBasePath, int retentionDays);
}