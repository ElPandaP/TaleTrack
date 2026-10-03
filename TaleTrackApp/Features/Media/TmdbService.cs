using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TaleTrackApp.Model;

namespace TaleTrackApp.Features.Media;

/// <summary>Film or series metadata found on TMDB. Any field can be missing.</summary>
public class TmdbResult
{
    /// <summary>Poster image URL on image.tmdb.org.</summary>
    public string? PosterUrl { get; set; }
    /// <summary>Synopsis in the detected language, or in the other one if TMDB has none in it.</summary>
    public string? Description { get; set; }
    /// <summary>Runtime in minutes: the film's, or a typical episode's for a series.</summary>
    public int? RuntimeMinutes { get; set; }
    /// <summary>Title translated to English, when the source was Spanish and TMDB has that translation.</summary>
    public string? TitleEN { get; set; }
    /// <summary>Title translated to Spanish, when the source was English and TMDB has that translation.</summary>
    public string? TitleES { get; set; }
    /// <summary>Episode count per season (index 0 = season 1). Series only.</summary>
    public int[]? SeasonEpisodeCounts { get; set; }
}

/// <summary>
/// Looks a film or series up on TMDB to find its poster, synopsis, runtime, episode counts and its
/// title in the other language (English or Spanish).
/// </summary>
/// <remarks>
/// The title in the other language lets the same content be recognised again when a Netflix UI
/// language switch changes the title the extension reads. Only <c>es</c> and <c>en</c> are
/// supported as source languages; anything else is skipped. Without <c>TMDB_API_KEY</c> every
/// lookup returns null.
/// </remarks>
public class TmdbService(HttpClient http, ILogger<TmdbService> logger, IConfiguration config)
{
    /// <summary>TMDB API key from <c>TMDB_API_KEY</c>. When empty, lookups are skipped.</summary>
    private readonly string? _apiKey = config["TMDB_API_KEY"];

    /// <summary>TMDB locale for a language code: <c>es-ES</c> for Spanish, <c>en-US</c> otherwise.</summary>
    private static string Locale(string lang) => lang == "es" ? "es-ES" : "en-US";
    /// <summary>The other supported language: <c>en</c> for <c>es</c> and the reverse.</summary>
    private static string OtherLanguage(string lang) => lang == "es" ? "en" : "es";

    /// <summary>
    /// Searches TMDB for <paramref name="title"/> and returns the metadata of the top result.
    /// </summary>
    /// <param name="title">Title as reported by the client.</param>
    /// <param name="type">Movie or series; any other type is skipped.</param>
    /// <param name="detectedLanguage">Language of <paramref name="title"/>, <c>es</c> or <c>en</c>.</param>
    /// <returns>The metadata found, or null if the lookup was skipped, found nothing or failed.</returns>
    public async Task<TmdbResult?> EnrichAsync(string title, MediaType type, string? detectedLanguage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return null;
        if (detectedLanguage != "es" && detectedLanguage != "en") return null;
        if (type != MediaType.Movie && type != MediaType.Series) return null;

        var isMovie = type == MediaType.Movie;
        var mediaPath = isMovie ? "movie" : "tv";
        var locale = Locale(detectedLanguage);
        var otherLang = OtherLanguage(detectedLanguage);

        try
        {
            var searchUrl =
                $"https://api.themoviedb.org/3/search/{mediaPath}?api_key={_apiKey}&language={locale}&query={Uri.EscapeDataString(title)}";
            var search = await http.GetFromJsonAsync<TmdbSearchResponse>(searchUrl);

            // Titles come straight from Netflix, so TMDB's top-ranked result is trusted as-is.
            var best = search?.Results?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(isMovie ? c.Title : c.Name));
            if (best == null)
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

            // Numbered seasons only (season 0 is "specials"), in order: index 0 is season 1.
            var seasonEpisodeCounts = !isMovie
                ? detail.Seasons?
                    .Where(s => s.SeasonNumber >= 1)
                    .OrderBy(s => s.SeasonNumber)
                    .Select(s => s.EpisodeCount)
                    .ToArray()
                : null;

            logger.LogInformation("TMDB: matched '{Found}' for '{Query}'",
                isMovie ? detail.Title : detail.Name, title);

            var cleanAltTitle = string.IsNullOrWhiteSpace(altTitle) ? null : altTitle;

            // TMDB returns an empty overview when the requested language has no translation.
            var overview = !string.IsNullOrWhiteSpace(detail.Overview) ? detail.Overview : altTranslation?.Data?.Overview;

            return new TmdbResult
            {
                PosterUrl = posterUrl,
                Description = string.IsNullOrWhiteSpace(overview) ? null : overview,
                RuntimeMinutes = runtime is > 0 ? runtime : null,
                TitleEN = otherLang == "en" ? cleanAltTitle : null,
                TitleES = otherLang == "es" ? cleanAltTitle : null,
                SeasonEpisodeCounts = seasonEpisodeCounts is { Length: > 0 } ? seasonEpisodeCounts : null,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning("TMDB lookup failed for '{Title}': {Msg}", title, ex.Message);
            return null;
        }
    }

    /// <summary>TMDB search results.</summary>
    private class TmdbSearchResponse
    {
        [JsonPropertyName("results")] public TmdbSearchResult[]? Results { get; set; }
    }

    /// <summary>One TMDB search result. Films use <c>title</c> and series use <c>name</c>.</summary>
    private class TmdbSearchResult
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }        // movies
        [JsonPropertyName("name")] public string? Name { get; set; }          // tv
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
    }

    /// <summary>The parts of a TMDB film or series detail that are used, including its translations.</summary>
    private class TmdbDetailResponse
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("runtime")] public int? Runtime { get; set; }                    // movies, minutes
        [JsonPropertyName("episode_run_time")] public int[]? EpisodeRunTime { get; set; }  // tv, minutes
        [JsonPropertyName("translations")] public TmdbTranslations? Translations { get; set; }
        [JsonPropertyName("seasons")] public TmdbSeason[]? Seasons { get; set; }           // tv only
    }

    /// <summary>One season of a series, with its episode count.</summary>
    private class TmdbSeason
    {
        [JsonPropertyName("season_number")] public int SeasonNumber { get; set; }
        [JsonPropertyName("episode_count")] public int EpisodeCount { get; set; }
    }

    /// <summary>Wrapper of the translations appended to a TMDB detail.</summary>
    private class TmdbTranslations
    {
        [JsonPropertyName("translations")] public TmdbTranslation[]? Translations { get; set; }
    }

    /// <summary>A translation of a film or series into one language.</summary>
    private class TmdbTranslation
    {
        [JsonPropertyName("iso_639_1")] public string? Iso6391 { get; set; }
        [JsonPropertyName("data")] public TmdbTranslationData? Data { get; set; }
    }

    /// <summary>The translated title and synopsis.</summary>
    private class TmdbTranslationData
    {
        [JsonPropertyName("title")] public string? Title { get; set; } // movies
        [JsonPropertyName("name")] public string? Name { get; set; }   // tv
        [JsonPropertyName("overview")] public string? Overview { get; set; }
    }
}
