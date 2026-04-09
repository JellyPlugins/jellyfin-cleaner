using System.Collections.Generic;
using Jellyfin.Plugin.JellyfinHelper.Services;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests;

public class I18nServiceTests
{
    // ===== SupportedLanguages Tests =====

    [Fact]
    public void SupportedLanguages_ContainsExpectedLanguages()
    {
        var languages = I18nService.SupportedLanguages;
        Assert.Contains("en", languages);
        Assert.Contains("de", languages);
        Assert.Contains("fr", languages);
        Assert.Contains("es", languages);
        Assert.Contains("pt", languages);
        Assert.Contains("zh", languages);
        Assert.Contains("tr", languages);
        Assert.Equal(7, languages.Count);
    }

    // ===== GetTranslations Tests =====

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("pt")]
    [InlineData("zh")]
    [InlineData("tr")]
    public void GetTranslations_SupportedLanguage_ReturnsDictionary(string lang)
    {
        var translations = I18nService.GetTranslations(lang);

        Assert.NotNull(translations);
        Assert.NotEmpty(translations);
        Assert.True(translations.ContainsKey("title"), $"Language '{lang}' is missing 'title' key");
        Assert.True(translations.ContainsKey("scanLibraries"), $"Language '{lang}' is missing 'scanLibraries' key");
    }

    [Fact]
    public void GetTranslations_NullLanguage_FallsBackToEnglish()
    {
        var translations = I18nService.GetTranslations(null);
        var english = I18nService.GetTranslations("en");

        Assert.Equal(english["title"], translations["title"]);
    }

    [Fact]
    public void GetTranslations_UnknownLanguage_FallsBackToEnglish()
    {
        var translations = I18nService.GetTranslations("xx");
        var english = I18nService.GetTranslations("en");

        Assert.Equal(english["title"], translations["title"]);
    }

    [Fact]
    public void GetTranslations_IsCaseInsensitive()
    {
        var lower = I18nService.GetTranslations("de");
        var upper = I18nService.GetTranslations("DE");

        Assert.Equal(lower["title"], upper["title"]);
    }

    [Fact]
    public void GetTranslations_EnglishHasAllExpectedKeys()
    {
        var translations = I18nService.GetTranslations("en");

        var expectedKeys = new[]
        {
            "title", "scanLibraries", "scanning", "scanDescription", "scanPlaceholder", "error",
            "tabOverview", "tabCodecs", "tabHealth", "tabTrends", "tabSettings", "tabArr",
            "movieVideoData", "tvVideoData", "musicAudio", "trickplayData", "subtitles", "totalFiles",
            "storageDistribution", "perLibrary",
            "cleanupStatistics", "totalBytesFreed", "totalItemsDeleted", "lastCleanup", "never",
            "videoCodecs", "audioCodecs", "containerFormats", "resolutions", "noData",
            "healthChecks", "noSubtitles", "noImages", "noNfo", "orphanedDirs",
            "growthTrend", "trendEmpty", "trendLoading", "trendError",
            "settingsTitle", "includedLibraries", "excludedLibraries",
            "orphanMinAge", "dryRunDefault", "enableSubtitleCleaner",
            "useTrash", "trashFolder", "trashRetention", "language",
            "radarrUrl", "radarrApiKey", "sonarrUrl", "sonarrApiKey",
            "saveSettings", "settingsSaved", "settingsError",
            "arrTitle", "compareRadarr", "compareSonarr",
            "inBoth", "inArrOnly", "inArrOnlyMissing", "inJellyfinOnly",
            "arrNotConfigured", "comparing",
            "exportJson", "exportCsv",
        };

        foreach (var key in expectedKeys)
        {
            Assert.True(translations.ContainsKey(key), $"English translations missing key: '{key}'");
            Assert.False(string.IsNullOrWhiteSpace(translations[key]), $"English translation for '{key}' is empty");
        }
    }

    [Fact]
    public void GetTranslations_GermanHasTitleTranslation()
    {
        var de = I18nService.GetTranslations("de");
        Assert.Equal("Jellyfin Helper — Medienstatistiken", de["title"]);
    }

    [Fact]
    public void GetTranslations_EachLanguageReturnsDistinctInstance()
    {
        var en1 = I18nService.GetTranslations("en");
        var en2 = I18nService.GetTranslations("en");

        // Should be separate instances (not the same reference)
        Assert.NotSame(en1, en2);
    }
}