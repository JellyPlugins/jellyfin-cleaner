using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Helper;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Tests.General;

[TestFixture]
public class GeneralTests : BaseUiTest
{
    [SetUp]
    public async Task Setup()
    {
        FileHelper.DeleteStatisticsLatestFile();
        await NavigationHelper.NavigateToPluginAsync();
        await NavigationHelper.NavigateToTabAsync("Overview");
    }

    [Test]
    public async Task General_ShouldDisplayPluginStartPage()
    {
        await AssertionsHelper.AssertElementToBeVisibleAsync("h2:has-text('Jellyfin Helper')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("span:has-text('🕒 Last Scan: just now')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("button.scan-libraries-btn");
        await AssertionsHelper.AssertTabIsActiveAsync("Overview");
    }

    [Test]
    public async Task General_ShouldDisplayTabBar()
    {
        await AssertionsHelper.AssertElementToBeVisibleAsync("div.tab-bar");
        await AssertionsHelper.AssertElementToBeVisibleAsync("button:has-text('📱 Overview')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("button:has-text('🎞️ Codecs')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("button:has-text('🩺 Health')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("button:has-text('📈 Trends')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("button:has-text('⚙️ Settings')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("button:has-text('🔗 Arr Integration')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("button:has-text('📋 Logs')");
    }

    [Test]
    public async Task General_ScanLibrary_LastScanBadge_ShouldDisplayJustNow()
    {
        await AssertionsHelper.AssertElementToBeVisibleAsync("span#lastScanBadge:has-text('🕒 Last Scan: just now')");
    }
}