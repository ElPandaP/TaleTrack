using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TaleTrackApp.Features.Media;

public class TmdbResult
{
    public string? PosterUrl { get; set; }
    public int? RuntimeMinutes { get; set; }
    /// <summary>The title in the other language (en↔es), when TMDB has that translation.</summary>
    public string? AltTitle { get; set; }
    /// <summary>Episode count per season (index 0 = season 1). Series only.</summary>
    public int[]? SeasonEpisodeCounts { get; set; }
}

/// <summary>
/// Looks up a movie/series on TMDB to fill in the poster, runtime and the title in
/// the "other" language (en/es) — the latter lets us recognise the same content again
/// after a Netflix UI language switch changes the title text we scrape.
/// Only handles "es"/"en" as the detected language; anything else is skipped, per plan.
/// </summary>
public class TmdbService(HttpClient http, ILogger<TmdbService> logger, IConfiguration config)
{
    private readonly string? _apiKey = config["TMDB_API_KEY"];

    private static string Locale(string lang) => lang == "es" ? "es-ES" : "en-US";
    private static string OtherLanguage(string lang) => lang == "es" ? "en" : "es";

    public async Task<TmdbResult?> EnrichAsync(string title, string type, string? detectedLanguage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return null;
        if (detectedLanguage != "es" && detectedLanguage != "en") return null;
        if (type != "Movie" && type != "Series") return null;

        var isMovie = type == "Movie";
        var mediaPath = isMovie ? "movie" : "tv";
        var locale = Locale(detectedLanguage);
        var otherLang = OtherLanguage(detectedLanguage);

        try
        {
            var searchUrl =
                $"https://api.themoviedb.org/3/search/{mediaPath}?api_key={_apiKey}&language={locale}&query={Uri.EscapeDataString(title)}";
            var search = await http.GetFromJsonAsync<TmdbSearchResponse>(searchUrl);

            TmdbSearchResult? best = null;
            double bestScore = 0;
            foreach (var candidate in search?.Results ?? [])
            {
                var candidateTitle = isMovie ? candidate.Title : candidate.Name;
                if (string.IsNullOrWhiteSpace(candidateTitle)) continue;
                var score = OpenLibraryService.NormalizedTokenSimilarity(title, candidateTitle);
                if (score > bestScore) { bestScore = score; best = candidate; }
            }

            if (best == null || bestScore < 0.5)
            {
                logger.LogInformation("TMDB: no match for '{Title}' ({Type})", title, type);
                return null;
            }

            var detailUrl =
                $"https://api.themoviedb.org/3/{mediaPath}/{best.Id}?api_key={_apiKey}&language={locale}&append_to_response=translations";
            var detail = await http.GetFromJsonAsync<TmdbDetailResponse>(detailUrl);
            if (detail == null) return null;

            var altTranslation = detail.Translations?.Translations
                ?.FirstOrDefault(t => t.Iso6391 == otherLang);
            var altTitle = isMovie ? altTranslation?.Data?.Title : altTranslation?.Data?.Name;

            var posterPath = detail.PosterPath ?? best.PosterPath;
            var posterUrl = string.IsNullOrWhiteSpace(posterPath)
                ? null
                : $"https://image.tmdb.org/t/p/w500{posterPath}";

            var runtime = isMovie ? detail.Runtime : detail.EpisodeRunTime?.FirstOrDefault(r => r > 0);

            // Numbered seasons only (season 0 is "specials"), in order — index 0 = season 1.
            var seasonEpisodeCounts = !isMovie
                ? detail.Seasons?
                    .Where(s => s.SeasonNumber >= 1)
                    .OrderBy(s => s.SeasonNumber)
                    .Select(s => s.EpisodeCount)
                    .ToArray()
                : null;

            logger.LogInformation("TMDB: matched '{Found}' ({Score:P0}) for '{Query}'",
                isMovie ? detail.Title : detail.Name, bestScore, title);

            return new TmdbResult
            {
                PosterUrl = posterUrl,
                RuntimeMinutes = runtime is > 0 ? runtime : null,
                AltTitle = string.IsNullOrWhiteSpace(altTitle) ? null : altTitle,
                SeasonEpisodeCounts = seasonEpisodeCounts is { Length: > 0 } ? seasonEpisodeCounts : null,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning("TMDB lookup failed for '{Title}': {Msg}", title, ex.Message);
            return null;
        }
    }

    private class TmdbSearchResponse
    {
        [JsonPropertyName("results")] public TmdbSearchResult[]? Results { get; set; }
    }

    private class TmdbSearchResult
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }        // movies
        [JsonPropertyName("name")] public string? Name { get; set; }          // tv
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
    }

    private class TmdbDetailResponse
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("runtime")] public int? Runtime { get; set; }                    // movies, minutes
        [JsonPropertyName("episode_run_time")] public int[]? EpisodeRunTime { get; set; }  // tv, minutes
        [JsonPropertyName("translations")] public TmdbTranslations? Translations { get; set; }
        [JsonPropertyName("seasons")] public TmdbSeason[]? Seasons { get; set; }           // tv only
    }

    private class TmdbSeason
    {
        [JsonPropertyName("season_number")] public int SeasonNumber { get; set; }
        [JsonPropertyName("episode_count")] public int EpisodeCount { get; set; }
    }

    private class TmdbTranslations
    {
        [JsonPropertyName("translations")] public TmdbTranslation[]? Translations { get; set; }
    }

    private class TmdbTranslation
    {
        [JsonPropertyName("iso_639_1")] public string? Iso6391 { get; set; }
        [JsonPropertyName("data")] public TmdbTranslationData? Data { get; set; }
    }

    private class TmdbTranslationData
    {
        [JsonPropertyName("title")] public string? Title { get; set; } // movies
        [JsonPropertyName("name")] public string? Name { get; set; }   // tv
    }
}
