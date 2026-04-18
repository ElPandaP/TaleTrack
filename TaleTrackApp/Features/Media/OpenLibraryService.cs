using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TaleTrackApp.Features.Media;

public class OpenLibraryResult
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? CoverUrl { get; set; }
    public string? Isbn { get; set; }
}

public class OpenLibraryService(HttpClient http, ILogger<OpenLibraryService> logger, IConfiguration config)
{
    private readonly double _threshold = config.GetValue<double>("OpenLibrary:SimilarityThreshold", 0.75);

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

            return new OpenLibraryResult { Title = doc.Title, Author = authorName, CoverUrl = coverUrl, Isbn = clean };
        }
        catch (Exception ex)
        {
            logger.LogWarning("OpenLibrary ISBN fetch failed for {Isbn}: {Msg}", isbn, ex.Message);
            return null;
        }
    }

    private async Task<string?> FetchAuthorNameAsync(string authorKey)
    {
        try
        {
            var a = await http.GetFromJsonAsync<AuthorResponse>($"https://openlibrary.org{authorKey}.json");
            return a?.Name;
        }
        catch { return null; }
    }

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
                Title = best.Title,
                Author = best.AuthorName?.FirstOrDefault(),
                CoverUrl = best.CoverId > 0 ? $"https://covers.openlibrary.org/b/id/{best.CoverId}-L.jpg" : null,
                Isbn = best.Isbn?.FirstOrDefault(),
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning("OpenLibrary search failed for '{Title}': {Msg}", title, ex.Message);
            return null;
        }
    }

    internal static double NormalizedTokenSimilarity(string a, string b)
    {
        var ta = Tokenize(a);
        var tb = Tokenize(b);
        if (ta.Count == 0 && tb.Count == 0) return 1.0;
        if (ta.Count == 0 || tb.Count == 0) return 0.0;
        var intersection = ta.Intersect(tb).Count();
        var union = ta.Union(tb).Count();
        return (double)intersection / union;
    }

    private static HashSet<string> Tokenize(string s) =>
        s.ToLowerInvariant()
         .Split([' ', '-', ':', ',', '.', '\'', '"', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries)
         .ToHashSet();

    private class IsbnResponse
    {
        [JsonPropertyName("title")]   public string? Title   { get; set; }
        [JsonPropertyName("covers")]  public int[]?  Covers  { get; set; }
        [JsonPropertyName("authors")] public AuthorRef[]? Authors { get; set; }
    }
    private class AuthorRef  { [JsonPropertyName("key")]  public string Key  { get; set; } = ""; }
    private class AuthorResponse { [JsonPropertyName("name")] public string? Name { get; set; } }
    private class SearchResponse { [JsonPropertyName("docs")] public SearchDoc[]? Docs { get; set; } }
    private class SearchDoc
    {
        [JsonPropertyName("title")]       public string?   Title      { get; set; }
        [JsonPropertyName("author_name")] public string[]? AuthorName { get; set; }
        [JsonPropertyName("cover_i")]     public int       CoverId    { get; set; }
        [JsonPropertyName("isbn")]        public string[]? Isbn       { get; set; }
    }
}
