namespace Jellyfin.Plugin.JellyfinHelper.Api;

/// <summary>
/// Request DTO for updating only the plugin log level via PUT /Configuration/LogLevel.
/// </summary>
public class LogLevelUpdateRequest
{
    /// <summary>
    /// Gets or sets the plugin log level (e.g. DEBUG, INFO, WARN, ERROR).
    /// </summary>
    public string PluginLogLevel { get; set; } = "INFO";
}