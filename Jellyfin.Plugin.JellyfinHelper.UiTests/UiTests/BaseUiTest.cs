using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Config;
using Microsoft.Playwright;
using NUnit.Framework.Interfaces;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests;

public class BaseUiTest
{
    [TearDown]
    public async Task TearDown()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed &&
            TestConfiguration.SaveScreenshotsOnFailure) await SaveScreenshot();
    }

    private static async Task SaveScreenshot()
    {
        var screenshotDir = Path.Combine(TestConfiguration.ProjectDir, TestConfiguration.ScreenshotPath);
        if (!Directory.Exists(screenshotDir)) Directory.CreateDirectory(screenshotDir);

        var testName = TestContext.CurrentContext.Test.MethodName;
        var className = TestContext.CurrentContext.Test.ClassName?.Split('.').Last();
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = $"{className}_{testName}_{timestamp}.png";
        var filePath = Path.Combine(screenshotDir, fileName);

        await TestConfiguration.Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = filePath,
            FullPage = true
        });
    }
}