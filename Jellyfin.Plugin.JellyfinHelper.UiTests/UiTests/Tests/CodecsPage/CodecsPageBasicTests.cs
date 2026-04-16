using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Helper;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Tests.CodecsPage;

[TestFixture]
public class CodecsPageBasicTests : BaseUiTest
{
    [SetUp]
    public async Task Setup()
    {
        FileHelper.DeleteStatisticsLatestFile();
        await NavigationHelper.NavigateToPluginAsync();
        await NavigationHelper.NavigateToTabAsync("Codecs");
    }

    [Test]
    public async Task CodecsPage_ShouldDisplayDataAfterLibraryScan()
    {
        await BasicHelper.ScanLibraries();

        await AssertionsHelper.AssertChartBox("Video Codecs", true);
        await AssertionsHelper.AssertChartBox("Container Formats", true);
        await AssertionsHelper.AssertChartBox("Resolutions", true);
    }
}