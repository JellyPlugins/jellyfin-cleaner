using Jellyfin.Plugin.JellyfinHelper.Configuration;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests.Services.Cleanup;

/// <summary>
/// Tests for <see cref="CleanupConfigHelper"/>.
/// Uses a real instance with a known PluginConfiguration instead of the removed ConfigOverride.
/// </summary>
public class CleanupConfigHelperTests
{
    private readonly CleanupConfigHelper _configHelper;

    public CleanupConfigHelperTests()
    {
        _configHelper = new CleanupConfigHelper();
    }

    // ===== ParseCommaSeparated Tests =====

    [Fact]
    public void ParseCommaSeparated_NullInput_ReturnsEmptySet()
    {
        var result = CleanupConfigHelper.ParseCommaSeparated(null);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseCommaSeparated_EmptyString_ReturnsEmptySet()
    {
        var result = CleanupConfigHelper.ParseCommaSeparated("");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseCommaSeparated_WhitespaceOnly_ReturnsEmptySet()
    {
        var result = CleanupConfigHelper.ParseCommaSeparated("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseCommaSeparated_SingleValue_ReturnsSingleItemSet()
    {
        var result = CleanupConfigHelper.ParseCommaSeparated("Movies");
        Assert.Single(result);
        Assert.Contains("Movies", result);
    }

    [Fact]
    public void ParseCommaSeparated_MultipleValues_ReturnsAllItems()
    {
        var result = CleanupConfigHelper.ParseCommaSeparated("Movies, TV Shows, Music");
        Assert.Equal(3, result.Count);
        Assert.Contains("Movies", result);
        Assert.Contains("TV Shows", result);
        Assert.Contains("Music", result);
    }

    [Fact]
    public void ParseCommaSeparated_TrimsWhitespace()
    {
        var result = CleanupConfigHelper.ParseCommaSeparated("  Movies  ,  TV Shows  ");
        Assert.Equal(2, result.Count);
        Assert.Contains("Movies", result);
        Assert.Contains("TV Shows", result);
    }

    [Fact]
    public void ParseCommaSeparated_IgnoresEmptyEntries()
    {
        var result = CleanupConfigHelper.ParseCommaSeparated("Movies,,, TV Shows,  ,Music");
        Assert.Equal(3, result.Count);
        Assert.Contains("Movies", result);
        Assert.Contains("TV Shows", result);
        Assert.Contains("Music", result);
    }

    [Fact]
    public void ParseCommaSeparated_IsCaseInsensitive()
    {
        var result = CleanupConfigHelper.ParseCommaSeparated("Movies");
        Assert.Contains("movies", result);
        Assert.Contains("MOVIES", result);
    }

    // ===== GetTrashPath Tests =====

    [Fact]
    public void GetTrashPath_DefaultConfig_ReturnsRelativeTrashPath()
    {
        // When Plugin.Instance is null, GetConfig() returns new PluginConfiguration()
        // which has TrashFolderPath = ".jellyfin-trash"
        var result = _configHelper.GetTrashPath("/media/movies");

        // On Windows the path separator differs, so just check it contains the expected components
        Assert.Contains(".jellyfin-trash", result);
    }

    // ===== Per-Task DryRun Tests =====

    [Fact]
    public void IsDryRunTrickplay_DefaultConfig_ReturnsTrue()
    {
        // When Plugin.Instance is null, config defaults have DryRunTrickplay = true
        Assert.True(_configHelper.IsDryRunTrickplay());
    }

    [Fact]
    public void IsDryRunEmptyMediaFolders_DefaultConfig_ReturnsTrue()
    {
        Assert.True(_configHelper.IsDryRunEmptyMediaFolders());
    }

    [Fact]
    public void IsDryRunOrphanedSubtitles_DefaultConfig_ReturnsTrue()
    {
        Assert.True(_configHelper.IsDryRunOrphanedSubtitles());
    }
}