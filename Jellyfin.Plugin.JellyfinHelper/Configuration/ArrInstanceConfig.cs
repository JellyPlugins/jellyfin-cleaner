namespace Jellyfin.Plugin.JellyfinHelper.Configuration;

/// <summary>
///     Represents a single Radarr or Sonarr instance configuration.
/// </summary>
public class ArrInstanceConfig
{
    /// <summary>
    ///     Gets the display name for this instance (e.g. "Radarr 4K", "Sonarr Anime").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the base URL (e.g., http://localhost:7878).
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the API key.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;
}