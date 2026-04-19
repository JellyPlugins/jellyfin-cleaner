using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Seerr;

/// <summary>
///     Represents a single media request from the Seerr API.
/// </summary>
internal sealed class SeerrRequest
{
    /// <summary>
    ///     Gets or sets the unique request ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the creation timestamp of the request (ISO 8601 UTC).
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    ///     Gets or sets the request status (1 = pending, 2 = approved, 3 = declined).
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    ///     Gets or sets the associated media information.
    /// </summary>
    [JsonPropertyName("media")]
    public SeerrMedia? Media { get; set; }
}