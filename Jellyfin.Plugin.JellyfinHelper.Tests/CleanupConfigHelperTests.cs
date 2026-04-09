using System;
using System.Collections.Generic;
using Jellyfin.Plugin.JellyfinHelper.Services;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests;

public class CleanupConfigHelperTests
{
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
        var result = CleanupConfigHelper.GetTrashPath("/media/movies");

        // On Windows the path separator differs, so just check it contains the expected components
        Assert.Contains(".jellyfin-trash", result);
    }

    // ===== IsEffectiveDryRun Tests =====

    [Fact]
    public void IsEffectiveDryRun_ExplicitTrue_ReturnsTrue()
    {
        // Explicit dry run always returns true regardless of config
        Assert.True(CleanupConfigHelper.IsEffectiveDryRun(true));
    }

    [Fact]
    public void IsEffectiveDryRun_ExplicitFalse_ReturnsDryRunByDefault()
    {
        // When Plugin.Instance is null, config.DryRunByDefault defaults to false
        Assert.False(CleanupConfigHelper.IsEffectiveDryRun(false));
    }
}