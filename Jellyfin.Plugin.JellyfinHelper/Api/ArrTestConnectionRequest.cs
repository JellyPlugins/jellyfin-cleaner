namespace Jellyfin.Plugin.JellyfinHelper.Api;

/// <summary>
///     Request model for testing an Arr connection.
/// </summary>
public class ArrTestConnectionRequest
{
    /// <summary>
    ///     Gets the base URL of the Arr instance.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    ///     Gets the API key.
    /// </summary>
    public string? ApiKey { get; init; }
}