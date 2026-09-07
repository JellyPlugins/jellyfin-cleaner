namespace Jellyfin.Plugin.JellyfinHelper.Api;

/// <summary>Response for GET /JellyfinHelper/Language.</summary>
public sealed class LanguageResponse
{
    /// <summary>Gets or sets the resolved UI language code (e.g. "en", "de", "sv").</summary>
    public string Language { get; set; } = "en";
}
