using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaleTrackApp.Features.Media;

/// <summary>Book metadata found on Open Library. Any field can be missing.</summary>
public class OpenLibraryResult
{
    /// <summary>Name of the first author.</summary>
    public string? Author { get; set; }
    /// <summary>Cover image URL on covers.openlibrary.org.</summary>
    public string? CoverUrl { get; set; }
    /// <summary>ISBN of the matched edition.</summary>
    public string? Isbn { get; set; }
    /// <summary>Synopsis of the book.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Looks books up on Open Library (by ISBN, or by title and author) to find their author, cover,
/// ISBN and synopsis.
/// </summary>
public class OpenLibraryService(HttpClient http, ILogger<OpenLibraryService> logger, IConfiguration config)
{
    /// <summary>
    /// Minimum title similarity (0 to 1) for a search result to count as a match. Read from
    /// <c>OpenLibrary:SimilarityThreshold</c>, 0.75 by default.
    /// </summary>
    private readonly double _threshold = config.GetValue<double>("OpenLibrary:SimilarityThreshold", 0.75);

    /// <summary>
    /// Finds a book's metadata: first by ISBN when there is one, then by searching title and author.
    /// </summary>
    /// <returns>The metadata found, or null if there is no good enough match or the lookup failed.</returns>
    public async Task<OpenLibraryResult?> EnrichAsync(string title, string? author, string? isbn)
    {
        if (!string.IsNullOrWhiteSpace(isbn))
        {
            var byIsbn = await FetchByIsbnAsync(isbn);
            if (byIsbn != null)
            {
                logger.LogInformation("OpenLibrary: ISBN hit for {Isbn}", isbn);
                return byIsbn;
            }
        }

        return await FetchBySearchAsync(title, author);
    }

    /// <summary>Fetches the edition with this ISBN, plus its author name and synopsis. Null if not found or on error.</summary>
    private async Task<OpenLibraryResult?> FetchByIsbnAsync(string isbn)
    {
        try
        {
            var clean = isbn.Replace("-", "").Replace(" ", "");
            var doc = await http.GetFromJsonAsync<IsbnResponse>($"https://openlibrary.org/isbn/{clean}.json");
            if (doc == null) return null;

            var coverUrl = doc.Covers?.FirstOrDefault(c => c > 0) is int coverId
                ? $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg"
                : null;

            string? authorName = null;
            if (doc.Authors?.Length > 0)
                authorName = await FetchAuthorNameAsync(doc.Authors[0].Key);

            // The synopsis lives on the work, shared by every edition; an edition may carry its own.
            var description = await FetchWorkDescriptionAsync(doc.Works?.FirstOrDefault()?.Key)
                ?? ReadText(doc.Description);

            return new OpenLibraryResult
            {
                Author = authorName, CoverUrl = coverUrl, Isbn = clean, Description = description,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning("OpenLibrary ISBN fetch failed for {Isbn}: {Msg}", isbn, ex.Message);
            return null;
        }
    }

    /// <summary>Name of the author behind an Open Library author key (e.g. <c>/authors/OL1A</c>). Null on error.</summary>
    private async Task<string?> FetchAuthorNameAsync(string authorKey)
    {
        try
        {
            var a = await http.GetFromJsonAsync<AuthorResponse>($"https://openlibrary.org{authorKey}.json");
            return a?.Name;
        }
        catch { return null; }
    }

    /// <summary>Synopsis of an Open Library work (e.g. <c>/works/OL1W</c>). Null if it has none or on error.</summary>
    private async Task<string?> FetchWorkDescriptionAsync(string? workKey)
    {
        if (string.IsNullOrWhiteSpace(workKey)) return null;
        try
        {
            var work = await http.GetFromJsonAsync<WorkResponse>($"https://openlibrary.org{workKey}.json");
            return work is null ? null : ReadText(work.Description);
        }
        catch (Exception ex)
        {
            logger.LogWarning("OpenLibrary work fetch failed for {Key}: {Msg}", workKey, ex.Message);
            return null;
        }
    }

    /// <summary>Open Library text fields are either a plain string or an object like { "type": "/type/text", "value": "..." }.</summary>
    private static string? ReadText(JsonElement field) => field.ValueKind switch
    {
        JsonValueKind.String => field.GetString(),
        JsonValueKind.Object when field.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String
            => value.GetString(),
        _ => null,
    };

    /// <summary>
    /// Searches by title (and author, when known) and keeps the result whose title is most similar,
    /// as long as it reaches <see cref="_threshold"/>. Null otherwise or on error.
    /// </summary>
    private async Task<OpenLibraryResult?> FetchBySearchAsync(string title, string? author)
    {
        try
        {
            var url = $"https://openlibrary.org/search.json?title={Uri.EscapeDataString(title)}&limit=5";
            if (!string.IsNullOrWhiteSpace(author))
                url += $"&author={Uri.EscapeDataString(author)}";

            var response = await http.GetFromJsonAsync<SearchResponse>(url);
            if (response?.Docs == null || response.Docs.Length == 0) return null;

            SearchDoc? best = null;
            double bestScore = 0;
            foreach (var doc in response.Docs)
            {
                if (string.IsNullOrWhiteSpace(doc.Title)) continue;
                var score = NormalizedTokenSimilarity(title, doc.Title);
                if (score > bestScore) { bestScore = score; best = doc; }
            }

            if (best == null || bestScore < _threshold)
            {
                logger.LogInformation("OpenLibrary: no match above {Threshold:P0} for '{Title}' (best={Score:P0})",
                    _threshold, title, bestScore);
                return null;
            }

            logger.LogInformation("OpenLibrary: matched '{Found}' ({Score:P0}) for '{Query}'",
                best.Title, bestScore, title);

            return new OpenLibraryResult
            {
                Author = best.AuthorName?.FirstOrDefault(),
                CoverUrl = best.CoverId > 0 ? $"https://covers.openlibrary.org/b/id/{best.CoverId}-L.jpg" : null,
                Isbn = best.Isbn?.FirstOrDefault(),
                Description = await FetchWorkDescriptionAsync(best.Key),
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning("OpenLibrary search failed for '{Title}': {Msg}", title, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Similarity of two titles from 0 to 1: the share of words they have in common (Jaccard index
    /// over lowercase words), so word order and punctuation do not matter.
    /// </summary>
    private static double NormalizedTokenSimilarity(string a, string b)
    {
        var ta = Tokenize(a);
        var tb = Tokenize(b);
        if (ta.Count == 0 && tb.Count == 0) return 1.0;
        if (ta.Count == 0 || tb.Count == 0) return 0.0;
        var intersection = ta.Intersect(tb).Count();
        var union = ta.Union(tb).Count();
        return (double)intersection / union;
    }

    /// <summary>Splits a title into its distinct lowercase words, ignoring common punctuation.</summary>
    private static HashSet<string> Tokenize(string s) =>
        s.ToLowerInvariant()
         .Split([' ', '-', ':', ',', '.', '\'', '"', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries)
         .ToHashSet();

    /// <summary>The parts of an Open Library edition (<c>/isbn/{isbn}.json</c>) that are used.</summary>
    private class IsbnResponse
    {
        [JsonPropertyName("title")]   public string? Title   { get; set; }
        [JsonPropertyName("covers")]  public int[]?  Covers  { get; set; }
        [JsonPropertyName("authors")] public KeyRef[]? Authors { get; set; }
        [JsonPropertyName("works")]   public KeyRef[]? Works   { get; set; }
        [JsonPropertyName("description")] public JsonElement Description { get; set; }
    }
    /// <summary>A reference to another Open Library record, by its key.</summary>
    private class KeyRef  { [JsonPropertyName("key")]  public string Key  { get; set; } = ""; }
    /// <summary>The parts of an Open Library work that are used.</summary>
    private class WorkResponse { [JsonPropertyName("description")] public JsonElement Description { get; set; } }
    /// <summary>The parts of an Open Library author that are used.</summary>
    private class AuthorResponse { [JsonPropertyName("name")] public string? Name { get; set; } }
    /// <summary>Open Library search results (<c>/search.json</c>).</summary>
    private class SearchResponse { [JsonPropertyName("docs")] public SearchDoc[]? Docs { get; set; } }
    /// <summary>One search result: a work, with data from its editions.</summary>
    private class SearchDoc
    {
        [JsonPropertyName("key")]         public string?   Key        { get; set; } // the work, e.g. /works/OL45804W
        [JsonPropertyName("title")]       public string?   Title      { get; set; }
        [JsonPropertyName("author_name")] public string[]? AuthorName { get; set; }
        [JsonPropertyName("cover_i")]     public int       CoverId    { get; set; }
        [JsonPropertyName("isbn")]        public string[]? Isbn       { get; set; }
    }
}
