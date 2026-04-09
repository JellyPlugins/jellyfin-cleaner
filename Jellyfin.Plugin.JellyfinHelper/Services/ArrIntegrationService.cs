using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Services;

/// <summary>
/// Provides integration with Radarr and Sonarr APIs to compare libraries.
/// </summary>
public class ArrIntegrationService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrIntegrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ArrIntegrationService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets all movies from Radarr.
    /// </summary>
    /// <param name="baseUrl">The Radarr base URL.</param>
    /// <param name="apiKey">The Radarr API key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of movies from Radarr.</returns>
    public async Task<List<ArrMovie>> GetRadarrMoviesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new List<ArrMovie>();
        }

        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/v3/movie";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Api-Key", apiKey);

            var response = await SharedHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var movies = JsonSerializer.Deserialize<List<RadarrMovieDto>>(json, JsonOptions) ?? new List<RadarrMovieDto>();

            return movies.Select(m => new ArrMovie
            {
                Title = m.Title ?? string.Empty,
                Year = m.Year,
                ImdbId = m.ImdbId ?? string.Empty,
                TmdbId = m.TmdbId,
                HasFile = m.HasFile,
                Path = m.Path ?? string.Empty,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch movies from Radarr at {Url}", baseUrl);
            return new List<ArrMovie>();
        }
    }

    /// <summary>
    /// Gets all series from Sonarr.
    /// </summary>
    /// <param name="baseUrl">The Sonarr base URL.</param>
    /// <param name="apiKey">The Sonarr API key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of series from Sonarr.</returns>
    public async Task<List<ArrSeries>> GetSonarrSeriesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new List<ArrSeries>();
        }

        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/v3/series";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Api-Key", apiKey);

            var response = await SharedHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var series = JsonSerializer.Deserialize<List<SonarrSeriesDto>>(json, JsonOptions) ?? new List<SonarrSeriesDto>();

            return series.Select(s => new ArrSeries
            {
                Title = s.Title ?? string.Empty,
                Year = s.Year,
                ImdbId = s.ImdbId ?? string.Empty,
                TvdbId = s.TvdbId,
                Path = s.Path ?? string.Empty,
                EpisodeFileCount = s.Statistics?.EpisodeFileCount ?? 0,
                TotalEpisodeCount = s.Statistics?.TotalEpisodeCount ?? 0,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch series from Sonarr at {Url}", baseUrl);
            return new List<ArrSeries>();
        }
    }

    /// <summary>
    /// Compares Radarr movies with Jellyfin library folder names.
    /// </summary>
    /// <param name="radarrMovies">Movies from Radarr.</param>
    /// <param name="jellyfinFolderNames">Set of folder names in Jellyfin movie libraries.</param>
    /// <returns>The comparison result.</returns>
    public static ArrComparisonResult CompareRadarrWithJellyfin(
        IReadOnlyList<ArrMovie> radarrMovies,
        HashSet<string> jellyfinFolderNames)
    {
        var result = new ArrComparisonResult();

        foreach (var movie in radarrMovies)
        {
            var folderName = System.IO.Path.GetFileName(movie.Path.TrimEnd('/').TrimEnd('\\'));
            if (string.IsNullOrEmpty(folderName))
            {
                continue;
            }

            if (jellyfinFolderNames.Contains(folderName))
            {
                result.InBoth.Add(movie.Title);
            }
            else if (movie.HasFile)
            {
                result.InArrOnly.Add($"{movie.Title} ({movie.Year}) — has file on disk");
            }
            else
            {
                result.InArrOnlyMissing.Add($"{movie.Title} ({movie.Year}) — no file");
            }
        }

        // Find Jellyfin-only items (not in Radarr)
        var radarrFolderNames = new HashSet<string>(
            radarrMovies
                .Select(m => System.IO.Path.GetFileName(m.Path.TrimEnd('/').TrimEnd('\\')))
                .Where(n => !string.IsNullOrEmpty(n)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var folderName in jellyfinFolderNames.Where(f => !radarrFolderNames.Contains(f)))
        {
            result.InJellyfinOnly.Add(folderName);
        }

        return result;
    }

    /// <summary>
    /// Compares Sonarr series with Jellyfin library folder names.
    /// </summary>
    /// <param name="sonarrSeries">Series from Sonarr.</param>
    /// <param name="jellyfinFolderNames">Set of folder names in Jellyfin TV libraries.</param>
    /// <returns>The comparison result.</returns>
    public static ArrComparisonResult CompareSonarrWithJellyfin(
        IReadOnlyList<ArrSeries> sonarrSeries,
        HashSet<string> jellyfinFolderNames)
    {
        var result = new ArrComparisonResult();

        foreach (var series in sonarrSeries)
        {
            var folderName = System.IO.Path.GetFileName(series.Path.TrimEnd('/').TrimEnd('\\'));
            if (string.IsNullOrEmpty(folderName))
            {
                continue;
            }

            if (jellyfinFolderNames.Contains(folderName))
            {
                result.InBoth.Add(series.Title);
            }
            else if (series.EpisodeFileCount > 0)
            {
                result.InArrOnly.Add($"{series.Title} ({series.Year}) — {series.EpisodeFileCount}/{series.TotalEpisodeCount} episodes on disk");
            }
            else
            {
                result.InArrOnlyMissing.Add($"{series.Title} ({series.Year}) — no episodes");
            }
        }

        var sonarrFolderNames = new HashSet<string>(
            sonarrSeries
                .Select(s => System.IO.Path.GetFileName(s.Path.TrimEnd('/').TrimEnd('\\')))
                .Where(n => !string.IsNullOrEmpty(n)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var folderName in jellyfinFolderNames.Where(f => !sonarrFolderNames.Contains(f)))
        {
            result.InJellyfinOnly.Add(folderName);
        }

        return result;
    }

    // --- DTOs for Radarr/Sonarr API responses ---

    private sealed class RadarrMovieDto
    {
        public string? Title { get; set; }

        public int Year { get; set; }

        public string? ImdbId { get; set; }

        public int TmdbId { get; set; }

        public bool HasFile { get; set; }

        public string? Path { get; set; }
    }

    private sealed class SonarrSeriesDto
    {
        public string? Title { get; set; }

        public int Year { get; set; }

        public string? ImdbId { get; set; }

        public int TvdbId { get; set; }

        public string? Path { get; set; }

        public SonarrStatisticsDto? Statistics { get; set; }
    }

    private sealed class SonarrStatisticsDto
    {
        public int EpisodeFileCount { get; set; }

        public int TotalEpisodeCount { get; set; }
    }
}
