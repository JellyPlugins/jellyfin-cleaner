using System.Text.RegularExpressions;
using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Config;
using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Dtos;
using Microsoft.Playwright;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Helper;

public static partial class AssertionsHelper
{
    [GeneratedRegex("^.*active.*$")]
    private static partial Regex ActiveRegex();

    /// <summary>
    ///     Asserts that an element is visible.
    /// </summary>
    /// <param name="locatorSelector">The CSS selector for the element to check.</param>
    /// <param name="parent">The parent locator to search within. Defaults to the body element if null.</param>
    /// <param name="timeout">
    ///     The maximum time to wait for the element to become visible, in milliseconds. Defaults to 15 seconds.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task AssertElementToBeVisibleAsync(string locatorSelector, ILocator? parent = null,
        int? timeout = 5000)
    {
        parent ??= TestConfiguration.Page.Locator("body");

        var element = parent.Locator(locatorSelector);
        await Assertions.Expect(element)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = timeout });
    }

    /// <summary>
    ///     Asserts that a tab is active.
    /// </summary>
    /// <param name="tabText">The text of the tab to check.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task AssertTabIsActiveAsync(string tabText)
    {
        var tabBar = TestConfiguration.Page.Locator("div.tab-bar");
        var tab = tabBar.Locator($"button:has-text('{tabText}')");

        await Assertions.Expect(tab).ToHaveClassAsync(ActiveRegex());
    }

    /// <summary>
    ///     Asserts the visibility of the statistical data card on the overview page based on the specified header,
    ///     value, and detail text.
    /// </summary>
    /// <param name="cardHeader">The header text of the statistical card.</param>
    /// <param name="cardValue">The value text displayed on the statistical card.</param>
    /// <param name="cardDetail">The detailed description text on the statistical card.</param>
    /// <param name="parent">The parent locator to search within, defaults to the body if null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task AssertStatsGridCard(string cardHeader, string cardValue, string? cardDetail = null,
        ILocator? parent = null)
    {
        parent ??= TestConfiguration.Page.Locator("body");

        var statsGrid = parent.Locator("div.stats-grid");
        var card = statsGrid.Locator("div.stat-card").Filter(new LocatorFilterOptions { HasText = cardHeader });

        await AssertElementToBeVisibleAsync($"p.stat-value:has-text('{cardValue}')", card);

        if (cardDetail == null)
            await Assertions.Expect(card.Locator("p.stat-detail")).ToHaveCountAsync(0);
        else
            await AssertElementToBeVisibleAsync($"p.stat-detail:has-text('{cardDetail}')", card);
    }

    /// <summary>
    ///     Asserts that the per-library breakdown table displays the expected rows with the correct data.
    /// </summary>
    /// <param name="expectedRows">
    ///     The list of expected rows, where each row represents a library and its associated data points.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task AssertPerLibraryBreakdown(List<PerLibraryBreakdownRow> expectedRows)
    {
        var table = TestConfiguration.Page.Locator("table.library-table");
        await table.ScrollIntoViewIfNeededAsync();

        var tbody = table.Locator("tbody");

        foreach (var expectedRow in expectedRows)
        {
            var row = tbody.Locator("tr").Filter(new LocatorFilterOptions
            {
                Has = tbody.Page.Locator($"td:first-child:has-text('{expectedRow.Library}')")
            });

            var tds = row.Locator("td");
            await AssertElementToBeVisibleAsync($"span:has-text('{expectedRow.Type}')", tds.Nth(1));
            await AssertElementToBeVisibleAsync($"text='{expectedRow.Video}'", tds.Nth(2));
            await AssertElementToBeVisibleAsync($"text='{expectedRow.Audio}'", tds.Nth(3));
            await AssertElementToBeVisibleAsync($"text='{expectedRow.Subtitles}'", tds.Nth(4));
            await AssertElementToBeVisibleAsync($"text='{expectedRow.Images}'", tds.Nth(5));
            await AssertElementToBeVisibleAsync($"text='{expectedRow.Trickplay}'", tds.Nth(6));
            await AssertElementToBeVisibleAsync($"text='{expectedRow.Total}'", tds.Nth(7));
        }
    }

    /// <summary>
    ///     Asserts the state of a chart box element on the page based on the presence or absence of data.
    /// </summary>
    /// <param name="chartBoxHeader">The text of the chart box header to locate the specific chart box.</param>
    /// <param name="noData">Indicates whether the chart box should display "No data".</param>
    /// <param name="parent">The parent locator to search within. Defaults to the body element if null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task AssertChartBox(string chartBoxHeader, bool noData, ILocator? parent = null)
    {
        parent ??= TestConfiguration.Page.Locator("body");

        var box = parent.Locator("div.chart-box").Filter(new LocatorFilterOptions { HasText = chartBoxHeader });

        if (noData)
            await AssertElementToBeVisibleAsync("p:has-text('No data')", box);
        else
            await Assertions.Expect(box.Locator("p")).ToHaveCountAsync(0);
    }

    /// <summary>
    ///     Asserts that a health grid item with the specified label and value is present and visible.
    /// </summary>
    /// <param name="itemLabel">The label of the health grid item to check.</param>
    /// <param name="itemValue">The expected value of the health grid item.</param>
    /// <param name="parent">The parent locator to search within. Defaults to the body element if null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task AssertHealthGridItem(string itemLabel, long itemValue, ILocator? parent = null)
    {
        parent ??= TestConfiguration.Page.Locator("body");

        var grid = parent.Locator("div.health-grid");
        var item = grid.Locator("div.health-item").Filter(new LocatorFilterOptions { HasText = itemLabel });

        await AssertElementToBeVisibleAsync($"div.health-value:has-text('{itemValue}')", item);
    }
}