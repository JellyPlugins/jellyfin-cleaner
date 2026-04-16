using Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Config;

namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Helper;

public static class FileHelper
{
    /// <summary>
    /// Navigates to the plugin main page (overview).
    /// </summary>
    public static void DeleteStatisticsLatestFile()
    {
        var fileToDelete = Path.GetFullPath(Path.Combine(TestConfiguration.ProjectDir,
            TestConfiguration.PathToPluginConfigFilesFolder, "jellyfin-helper-statistics-latest.json"));
        if (File.Exists(fileToDelete))
        {
            File.Delete(fileToDelete);
        }
    }
}