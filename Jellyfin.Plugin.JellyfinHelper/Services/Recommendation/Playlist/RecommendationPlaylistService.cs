using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.JellyfinHelper.Services.PluginLog;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Playlist;

/// <summary>
///     Creates and manages per-user recommendation playlists in Jellyfin.
///     Playlists are identified by a well-known name prefix so they can be
///     found and replaced on each scheduled run.
/// </summary>
public sealed class RecommendationPlaylistService : IRecommendationPlaylistService
{
    /// <summary>
    ///     The prefix used to identify recommendation playlists managed by this plugin.
    ///     This prefix is used for searching existing playlists to delete before recreation.
    ///     The full playlist name includes a dynamic date suffix.
    /// </summary>
    internal const string PlaylistNamePrefix = "🎬 Recommended";

    private readonly IPlaylistManager _playlistManager;
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IPluginLogService _pluginLog;
    private readonly ILogger<RecommendationPlaylistService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecommendationPlaylistService"/> class.
    /// </summary>
    /// <param name="playlistManager">The Jellyfin playlist manager.</param>
    /// <param name="userManager">The Jellyfin user manager.</param>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="pluginLog">The plugin log service.</param>
    /// <param name="logger">The logger instance.</param>
    public RecommendationPlaylistService(
        IPlaylistManager playlistManager,
        IUserManager userManager,
        ILibraryManager libraryManager,
        IPluginLogService pluginLog,
        ILogger<RecommendationPlaylistService> logger)
    {
        _playlistManager = playlistManager;
        _userManager = userManager;
        _libraryManager = libraryManager;
        _pluginLog = pluginLog;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PlaylistSyncResult> UpdatePlaylistsForAllUsersAsync(
        IReadOnlyList<RecommendationResult> results,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);

        var syncResult = new PlaylistSyncResult();
        var playlistName = BuildPlaylistName();

        _pluginLog.LogInfo(
            "PlaylistSync",
            $"Starting playlist sync for {results.Count} users. Playlist name: '{playlistName}'",
            _logger);

        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Remove any existing recommendation playlists for this user
                var removed = await RemoveUserPlaylistsAsync(result.UserId, cancellationToken).ConfigureAwait(false);
                syncResult.OldPlaylistsRemoved += removed;

                // Skip playlist creation if there are no recommendations
                if (result.Recommendations.Count == 0)
                {
                    _pluginLog.LogDebug(
                        "PlaylistSync",
                        $"No recommendations for user '{result.UserName}' â€” skipping playlist creation.",
                        _logger);
                    continue;
                }

                // Create new playlist with items in score-ranked order
                var itemIds = result.Recommendations
                    .OrderByDescending(r => r.Score)
                    .Select(r => r.ItemId)
                    .ToArray();

                var request = new PlaylistCreationRequest
                {
                    Name = playlistName,
                    UserId = result.UserId,
                    ItemIdList = itemIds,
                    MediaType = MediaType.Unknown // Mixed content (movies + series)
                };

                var playlistResult = await _playlistManager.CreatePlaylist(request).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(playlistResult.Id))
                {
                    syncResult.PlaylistsCreated++;
                    syncResult.TotalItemsAdded += itemIds.Length;

                    _pluginLog.LogDebug(
                        "PlaylistSync",
                        $"Created playlist '{playlistName}' for user '{result.UserName}' with {itemIds.Length} items.",
                        _logger);
                }
                else
                {
                    syncResult.PlaylistsFailed++;
                    _pluginLog.LogWarning(
                        "PlaylistSync",
                        $"Playlist creation returned empty ID for user '{result.UserName}'.",
                        logger: _logger);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                syncResult.PlaylistsFailed++;
                _pluginLog.LogWarning(
                    "PlaylistSync",
                    $"Failed to sync playlist for user '{result.UserName}'.",
                    ex,
                    _logger);
            }
        }

        _pluginLog.LogInfo(
            "PlaylistSync",
            $"Playlist sync complete: {syncResult.PlaylistsCreated} created, {syncResult.OldPlaylistsRemoved} old removed, {syncResult.PlaylistsFailed} failed, {syncResult.TotalItemsAdded} total items.",
            _logger);

        return syncResult;
    }

    /// <inheritdoc />
    public async Task<int> RemoveAllRecommendationPlaylistsAsync(CancellationToken cancellationToken = default)
    {
        var totalRemoved = 0;
        var users = _userManager.Users.ToList();

        _pluginLog.LogInfo(
            "PlaylistSync",
            $"Removing all recommendation playlists for {users.Count} users...",
            _logger);

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var removed = await RemoveUserPlaylistsAsync(user.Id, cancellationToken).ConfigureAwait(false);
                totalRemoved += removed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                _pluginLog.LogWarning(
                    "PlaylistSync",
                    $"Failed to remove playlists for user '{user.Username}'.",
                    ex,
                    _logger);
            }
        }

        _pluginLog.LogInfo(
            "PlaylistSync",
            $"Removed {totalRemoved} recommendation playlists.",
            _logger);

        return totalRemoved;
    }

    /// <summary>
    ///     Builds the dynamic playlist name including a date-based suffix.
    ///     Uses ISO week number for weekly scheduling context.
    ///     Example: "Recommended -- Week 17, 2026".
    /// </summary>
    /// <returns>The full playlist name.</returns>
    internal static string BuildPlaylistName()
    {
        return PlaylistNamePrefix + " for you";
    }

    /// <summary>
    ///     Finds and removes all recommendation playlists owned by the specified user.
    ///     Identifies managed playlists by the <see cref="PlaylistNamePrefix"/> prefix.
    /// </summary>
    /// <param name="userId">The user ID whose playlists to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of playlists removed.</returns>
    private Task<int> RemoveUserPlaylistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Find all playlists owned by this user that match our prefix
        var existingPlaylists = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Playlist],
            User = _userManager.GetUserById(userId),
            SearchTerm = PlaylistNamePrefix
        });

        var removed = 0;
        foreach (var playlist in existingPlaylists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Double-check the name starts with our prefix to avoid false positives
            if (playlist.Name == null || !playlist.Name.StartsWith(PlaylistNamePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            _libraryManager.DeleteItem(playlist, new DeleteOptions { DeleteFileLocation = true });
            removed++;

            _pluginLog.LogDebug(
                "PlaylistSync",
                $"Removed old playlist '{playlist.Name}' (ID: {playlist.Id}) for user {userId}.",
                _logger);
        }

        return Task.FromResult(removed);
    }
}