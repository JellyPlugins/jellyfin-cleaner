using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Seerr;

/// <summary>
///     Represents a paginated response from the Seerr /api/v1/request endpoint.
/// </summary>
internal sealed class SeerrRequestPage
{
    private List<SeerrRequest> _results = [];

    /// <summary>
    ///     Gets or sets the pagination info.
    /// </summary>
    [JsonPropertyName("pageInfo")]
    public SeerrPageInfo PageInfo { get; set; } = new();

    /// <summary>
    ///     Gets or sets the list of requests in this page.
    ///     Setter coalesces null to empty to prevent NRE from external API responses.
    /// </summary>
    [JsonPropertyName("results")]
    public List<SeerrRequest> Results
    {
        get => _results;
        set => _results = value ?? [];
    }
}
