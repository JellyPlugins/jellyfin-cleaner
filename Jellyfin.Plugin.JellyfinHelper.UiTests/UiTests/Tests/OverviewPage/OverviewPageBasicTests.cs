using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Config;
using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Dtos;
using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Helper;
using Microsoft.Playwright;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Tests.OverviewPage;

[TestFixture]
public class OverviewPageBasicTests : BaseUiTest
{
    [SetUp]
    public async Task Setup()
    {
        FileHelper.DeleteStatisticsLatestFile();
        await NavigationHelper.NavigateToPluginAsync();
        await NavigationHelper.NavigateToTabAsync("Overview");
    }

    [Test]
    public async Task OverviewPage_ShouldDisplayDataAfterLibraryScan()
    {
        await BasicHelper.ScanLibraries();

        // Statistics
        await AssertionsHelper.AssertStatsGridCard("Video Data — Movies", "0 B", "0 files across 2 libraries");
        await AssertionsHelper.AssertStatsGridCard("Video Data — TV Shows", "0 B", "0 episodes across 2 libraries");
        await AssertionsHelper.AssertStatsGridCard("Music / Audio", "0 B", "0 files");
        await AssertionsHelper.AssertStatsGridCard("Trickplay Data", "0 B", "0 folders");
        await AssertionsHelper.AssertStatsGridCard("Subtitles", "0 B", "0 files");
        await AssertionsHelper.AssertStatsGridCard("Total Files", "0 media files", "0 Video, 0 audio");

        // Storage Distribution
        var storageDistributionHeader =
            TestConfiguration.Page.Locator("div.section-title:has-text('⛃ Storage Distribution — ')");
        await AssertionsHelper.AssertElementToBeVisibleAsync("span:has-text('0 B Total')", storageDistributionHeader);

        // Per-Library Breakdown
        await AssertPerLibraryBreakdownTableHeader();

        List<PerLibraryBreakdownRow> expectedRows =
        [
            new("Anime Movies", "MOVIES", "0 B", "0 B", "0 B", "0 B", "0 B", "0 B"),
            new("Anime Series", "TV SHOWS", "0 B", "0 B", "0 B", "0 B", "0 B", "0 B"),
            new("Movies", "MOVIES", "0 B", "0 B", "0 B", "0 B", "0 B", "0 B"),
            new("Series", "TV SHOWS", "0 B", "0 B", "0 B", "0 B", "0 B", "0 B")
        ];
        await AssertionsHelper.AssertPerLibraryBreakdown(expectedRows);

        // Cleanup Statistics
        var cleanupStatisticsContainer = TestConfiguration.Page.Locator("div#cleanup-stats-container");
        await AssertionsHelper.AssertStatsGridCard("Total Space Freed", "0 B", null, cleanupStatisticsContainer);
        await AssertionsHelper.AssertStatsGridCard("Total Items Deleted", "0", "Last Cleanup: ",
            cleanupStatisticsContainer);
    }

    // Helper methods

    private static async Task AssertPerLibraryBreakdownTableHeader()
    {
        var table = TestConfiguration.Page.Locator("table.library-table");
        await table.ScrollIntoViewIfNeededAsync();

        var headers = table.Locator("thead").Locator("tr").Locator("th");

        var expectedHeaders = new[]
            { "Library", "Type", "Video", "Audio", "Subtitles", "Images", "Trickplay", "Total" };
        await Assertions.Expect(headers).ToHaveCountAsync(expectedHeaders.Length);

        for (var i = 0; i < expectedHeaders.Length; i++)
        {
            var actual = await headers.Nth(i).InnerTextAsync();
            Assert.That(actual, Is.EqualTo(expectedHeaders[i]).IgnoreCase);
        }
    }
}