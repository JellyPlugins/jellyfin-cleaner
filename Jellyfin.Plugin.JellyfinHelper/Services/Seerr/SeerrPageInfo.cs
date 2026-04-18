using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyfinHelper.Services.Seerr;

/// <summary>
///     Pagination metadata from the Seerr API.
/// </summary>
internal sealed class SeerrPageInfo
{
    /// <summary>
    ///     Gets or sets the current page number (1-based).
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    /// <summary>
    ///     Gets or sets the total number of pages.
    /// </summary>
    [JsonPropertyName("pages")]
    public int Pages { get; set; }

    /// <summary>
    ///     Gets or sets the total number of results across all pages.
    /// </summary>
    [JsonPropertyName("results")]
    public int Results { get; set; }

    /// <summary>
    ///     Gets or sets the number of results per page.
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}