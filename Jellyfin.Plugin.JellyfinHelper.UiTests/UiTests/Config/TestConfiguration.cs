using Microsoft.Playwright;
using IContainer = DotNet.Testcontainers.Containers.IContainer;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Config;

public static class TestConfiguration
{
    public static string PathToConfigFolder => "JellyfinServer/config/";
    public static string PathToMediaFolder => "JellyfinServer/media/";
    public static string PathToPluginConfigFilesFolder => PathToConfigFolder + "data/";
    public static string JellyfinUser => "Test";
    public static string JellyfinPassword => "Test";
    public static bool Headless => true;
    public static int SlowMo => 0;
    public static bool SaveScreenshotsOnFailure => true;
    public static string ScreenshotPath => "UiTests/Screenshots";

    public static string ProjectDir { get; set; } = "";
    public static string BaseUrl { get; set; } = "";
    public static IContainer JellyfinContainer { get; set; } = null!;
    public static IPlaywright Playwright { get; set; } = null!;
    public static IBrowser Browser { get; set; } = null!;
    public static IPage Page { get; set; } = null!;
}