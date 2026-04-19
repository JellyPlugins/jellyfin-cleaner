using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Seerr;

/// <summary>
///     Represents a paginated response from the Seerr /api/v1/request endpoint.
/// </summary>
internal sealed class SeerrRequestPage
{
    /// <summary>
    ///     Gets or sets the pagination info.
    /// </summary>
    [JsonPropertyName("pageInfo")]
    public SeerrPageInfo PageInfo { get; set; } = new();

    /// <summary>
    ///     Gets or sets the list of requests in this page.
    /// </summary>
    [JsonPropertyName("results")]
    public List<SeerrRequest> Results { get; set; } = [];
}