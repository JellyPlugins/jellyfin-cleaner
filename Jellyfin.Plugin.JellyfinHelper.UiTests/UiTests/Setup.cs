using Microsoft.Playwright;
using DotNet.Testcontainers.Builders;
using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Config;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests;

[SetUpFixture]
public class Setup
{
    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        TestConfiguration.ProjectDir =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

        var configPath =
            Path.GetFullPath(Path.Combine(TestConfiguration.ProjectDir, TestConfiguration.PathToConfigFolder));
        var mediaPath =
            Path.GetFullPath(Path.Combine(TestConfiguration.ProjectDir, TestConfiguration.PathToMediaFolder));

        TestConfiguration.JellyfinContainer = new ContainerBuilder()
            .WithImage("jellyfin/jellyfin:10.11.0")
            .WithPortBinding(8096, true)
            .WithBindMount(configPath, "/config")
            .WithBindMount(mediaPath, "/media")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*Core startup complete.*"))
            .Build();

        await TestConfiguration.JellyfinContainer.StartAsync();

        TestConfiguration.BaseUrl = $"http://localhost:{TestConfiguration.JellyfinContainer.GetMappedPublicPort(8096)}";
        TestConfiguration.Playwright = await Playwright.CreateAsync();

        TestConfiguration.Browser = await TestConfiguration.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = TestConfiguration.Headless,
            SlowMo = TestConfiguration.SlowMo
        });
        TestConfiguration.Page = await TestConfiguration.Browser.NewPageAsync();
        await LoginAsync();
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        await TestConfiguration.Browser.CloseAsync();
        TestConfiguration.Playwright.Dispose();

        await TestConfiguration.JellyfinContainer.StopAsync();
        await TestConfiguration.JellyfinContainer.DisposeAsync();
    }

    private static async Task LoginAsync()
    {
        await TestConfiguration.Page.GotoAsync($"{TestConfiguration.BaseUrl}/web/index.html#!/login.html");
        await TestConfiguration.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (TestConfiguration.Page.Url.Contains("index.html") && !TestConfiguration.Page.Url.Contains("login.html"))
        {
            // Already logged in
            return;
        }

        await TestConfiguration.Page.WaitForSelectorAsync("input[id='txtManualName']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await TestConfiguration.Page.FillAsync("input[id='txtManualName']", TestConfiguration.JellyfinUser);
        await TestConfiguration.Page.FillAsync("input[id='txtManualPassword']", TestConfiguration.JellyfinPassword);
        await TestConfiguration.Page.ClickAsync("button[type='submit']");

        await TestConfiguration.Page.WaitForURLAsync(url => !url.Contains("login.html"),
            new PageWaitForURLOptions { Timeout = 15000 });
        await TestConfiguration.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}