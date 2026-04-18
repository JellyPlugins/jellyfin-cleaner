using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Helper;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Tests.HealthPage;

[TestFixture]
public class HealthPageBasicTests : BaseUiTest
{
    [SetUp]
    public async Task Setup()
    {
        FileHelper.DeleteStatisticsLatestFile();
        await NavigationHelper.NavigateToPluginAsync();
        await NavigationHelper.NavigateToTabAsync("Health");
    }

    [Test]
    public async Task HealthPage_ShouldDisplayDataAfterLibraryScan()
    {
        await AssertionsHelper.AssertHealthGridItem("Videos without subtitles", 0);
        await AssertionsHelper.AssertHealthGridItem("Videos without images", 0);
        await AssertionsHelper.AssertHealthGridItem("Videos without NFO", 0);
        await AssertionsHelper.AssertHealthGridItem("Orphaned metadata dirs", 0);
    }
}