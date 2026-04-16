using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Config;
using Microsoft.Playwright;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Helper;

public static class BasicHelper
{
    /// <summary>
    ///     Clicks the "Scan Libraries" button and waits for the backend to finish scanning.
    /// </summary>
    public static async Task ScanLibraries()
    {
        var scanLibraryButton = TestConfiguration.Page.Locator("button:has-text('↻ Scan Libraries')");

        var success = false;
        var scanCount = 0;
        const int maxScanCount = 6;
        while (!success && scanCount <= maxScanCount)
        {
            await scanLibraryButton.ClickAsync();
            await TestConfiguration.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var failedMessage =
                TestConfiguration.Page.Locator(
                    "div.error-msg:has-text('Failed to load statistics. Make sure you are an administrator.')");
            success = !await failedMessage.IsVisibleAsync();

            if (success)
            {
                break;
            }

            scanCount++;
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        if (!success)
        {
            throw new Exception("Failed to scan libraries after multiple attempts.");
        }
    }
}