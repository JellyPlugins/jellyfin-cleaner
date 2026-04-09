namespace Jellyfin.Plugin.JellyfinHelper.Services;

/// <summary>
/// Represents a movie from Radarr.
/// </summary>
public class ArrMovie
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the year.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the IMDb ID.</summary>
    public string ImdbId { get; set; } = string.Empty;

    /// <summary>Gets or sets the TMDb ID.</summary>
    public int TmdbId { get; set; }

    /// <summary>Gets or sets a value indicating whether the movie has a file on disk.</summary>
    public bool HasFile { get; set; }

    /// <summary>Gets or sets the file path.</summary>
    public string Path { get; set; } = string.Empty;
}