using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Config;
using Microsoft.Playwright;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Helper;

public static class NavigationHelper
{
    /// <summary>
    /// Navigates to the plugin main page (overview).
    /// </summary>
    public static async Task NavigateToPluginAsync()
    {
        await TestConfiguration.Page.GotoAsync(
            $"{TestConfiguration.BaseUrl}/web/index.html#/configurationpage?name=Jellyfin%20Helper");
        await TestConfiguration.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public static async Task NavigateToTabAsync(string tabText)
    {
        var tabBar = TestConfiguration.Page.Locator("div.tab-bar");
        var tab = tabBar.Locator($"button:has-text('{tabText}')");
        await tab.ClickAsync();
    }
}