using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Seerr;

/// <summary>
///     Represents media information associated with a Seerr request.
/// </summary>
internal sealed class SeerrMedia
{
    /// <summary>
    ///     Gets or sets the media type ("movie" or "tv").
    /// </summary>
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the TMDB ID.
    /// </summary>
    [JsonPropertyName("tmdbId")]
    public int TmdbId { get; set; }

    /// <summary>
    ///     Gets or sets the media status in Seerr.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }
}