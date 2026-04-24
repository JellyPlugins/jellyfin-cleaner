using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyfinHelper.Services.PluginLog;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Playlist;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests.Services.Recommendation.Playlist;

/// <summary>
///     Unit tests for <see cref="RecommendationPlaylistService"/>.
/// </summary>
public class RecommendationPlaylistServiceTests
{
    private readonly Mock<IPlaylistManager> _playlistManagerMock = new();
    private readonly Mock<IUserManager> _userManagerMock = new();
    private readonly Mock<ILibraryManager> _libraryManagerMock = new();
    private readonly Mock<IPluginLogService> _pluginLogMock = new();
    private readonly Mock<ILogger<RecommendationPlaylistService>> _loggerMock = new();

    private RecommendationPlaylistService CreateSut() =>
        new(
            _playlistManagerMock.Object,
            _userManagerMock.Object,
            _libraryManagerMock.Object,
            _pluginLogMock.Object,
            _loggerMock.Object);

    private static RecommendationResult CreateResult(Guid userId, string userName, int itemCount)
    {
        var items = new Collection<RecommendedItem>();
        for (var i = 0; i < itemCount; i++)
        {
            items.Add(new RecommendedItem
            {
                ItemId = Guid.NewGuid(),
                Name = $"Item {i}",
                Score = 1.0 - (i * 0.05)
            });
        }

        return new RecommendationResult
        {
            UserId = userId,
            UserName = userName,
            Recommendations = items
        };
    }

    [Fact]
    public async Task UpdatePlaylists_CreatesPlaylistForEachUser()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var results = new List<RecommendationResult>
        {
            CreateResult(user1, "Alice", 5),
            CreateResult(user2, "Bob", 3)
        };

        _libraryManagerMock.Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem>());
        _playlistManagerMock.Setup(m => m.CreatePlaylist(It.IsAny<PlaylistCreationRequest>()))
            .ReturnsAsync(new PlaylistCreationResult(Guid.NewGuid().ToString()));

        var sut = CreateSut();

        // Act
        var syncResult = await sut.UpdatePlaylistsForAllUsersAsync(results, CancellationToken.None);

        // Assert
        Assert.Equal(2, syncResult.PlaylistsCreated);
        Assert.Equal(8, syncResult.TotalItemsAdded);
        Assert.Equal(0, syncResult.PlaylistsFailed);
    }

    [Fact]
    public async Task UpdatePlaylists_SkipsUsersWithNoRecommendations()
    {
        // Arrange
        var results = new List<RecommendationResult>
        {
            CreateResult(Guid.NewGuid(), "Alice", 0)
        };

        _libraryManagerMock.Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem>());

        var sut = CreateSut();

        // Act
        var syncResult = await sut.UpdatePlaylistsForAllUsersAsync(results, CancellationToken.None);

        // Assert
        Assert.Equal(0, syncResult.PlaylistsCreated);
        Assert.Equal(0, syncResult.TotalItemsAdded);
        _playlistManagerMock.Verify(
            m => m.CreatePlaylist(It.IsAny<PlaylistCreationRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePlaylists_HandlesCreationFailureGracefully()
    {
        // Arrange
        var results = new List<RecommendationResult>
        {
            CreateResult(Guid.NewGuid(), "Alice", 5)
        };

        _libraryManagerMock.Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem>());
        _playlistManagerMock.Setup(m => m.CreatePlaylist(It.IsAny<PlaylistCreationRequest>()))
            .ThrowsAsync(new InvalidOperationException("Playlist creation failed"));

        var sut = CreateSut();

        // Act
        var syncResult = await sut.UpdatePlaylistsForAllUsersAsync(results, CancellationToken.None);

        // Assert — graceful failure, no exception thrown
        Assert.Equal(0, syncResult.PlaylistsCreated);
        Assert.Equal(1, syncResult.PlaylistsFailed);
    }

    [Fact]
    public async Task UpdatePlaylists_CancellationRespected()
    {
        // Arrange
        var results = new List<RecommendationResult>
        {
            CreateResult(Guid.NewGuid(), "Alice", 5)
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.UpdatePlaylistsForAllUsersAsync(results, cts.Token));
    }

    [Fact]
    public async Task UpdatePlaylists_EmptyResultsList_Succeeds()
    {
        // Arrange
        var results = new List<RecommendationResult>();
        var sut = CreateSut();

        // Act
        var syncResult = await sut.UpdatePlaylistsForAllUsersAsync(results, CancellationToken.None);

        // Assert
        Assert.Equal(0, syncResult.PlaylistsCreated);
        Assert.Equal(0, syncResult.TotalItemsAdded);
        Assert.Equal(0, syncResult.PlaylistsFailed);
    }

    [Fact]
    public void BuildPlaylistName_ContainsPrefixAndForYou()
    {
        // Act
        var name = RecommendationPlaylistService.BuildPlaylistName();

        // Assert
        Assert.StartsWith(RecommendationPlaylistService.PlaylistNamePrefix, name);
        Assert.Contains("for you", name);
    }

    [Fact]
    public async Task UpdatePlaylists_NullResults_ThrowsArgumentNull()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.UpdatePlaylistsForAllUsersAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePlaylists_ItemsOrderedByScoreDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var result = CreateResult(userId, "Alice", 3);
        var results = new List<RecommendationResult> { result };

        _libraryManagerMock.Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem>());

        IReadOnlyList<Guid>? capturedItemIds = null;
        _playlistManagerMock.Setup(m => m.CreatePlaylist(It.IsAny<PlaylistCreationRequest>()))
            .Callback<PlaylistCreationRequest>(req => capturedItemIds = req.ItemIdList)
            .ReturnsAsync(new PlaylistCreationResult(Guid.NewGuid().ToString()));

        var sut = CreateSut();

        // Act
        await sut.UpdatePlaylistsForAllUsersAsync(results, CancellationToken.None);

        // Assert — items should be ordered by score descending
        Assert.NotNull(capturedItemIds);
        var expectedIds = result.Recommendations
            .OrderByDescending(r => r.Score)
            .Select(r => r.ItemId)
            .ToArray();
        Assert.Equal(expectedIds, capturedItemIds);
    }
}