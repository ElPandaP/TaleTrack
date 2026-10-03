using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Google.Apis.Auth;
using TaleTrackApp.Features.Auth;

namespace TaleTrackApp.Tests;

/// <summary>
/// Stand-in for Resend. It keeps every email the backend sends so tests can read the recipient,
/// the subject and the link inside, and it fails on purpose for any recipient that contains
/// <see cref="DownMarker"/>, to simulate Resend being down.
/// </summary>
public sealed class FakeResend
{
    /// <summary>Recipients containing this text get a 500 from the fake, as if Resend were down.</summary>
    public const string DownMarker = "resend-down";

    private readonly ConcurrentQueue<SentEmail> _sent = new();

    /// <summary>An email the backend sent.</summary>
    /// <param name="To">Recipient.</param>
    /// <param name="Subject">Subject line, which tells the language apart.</param>
    /// <param name="Html">Body.</param>
    public sealed record SentEmail(string To, string Subject, string Html)
    {
        /// <summary>The token of the link in the email (reset, delete or revoke), or null if it has none.</summary>
        public string? LinkToken
        {
            get
            {
                var match = Regex.Match(Html, "token=([^\"&]+)");
                return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
            }
        }

        /// <summary>The six-digit sign-in code in the email, or null if it has none.</summary>
        public string? Code
        {
            get
            {
                var match = Regex.Match(Html, @">(\d{6})<");
                return match.Success ? match.Groups[1].Value : null;
            }
        }
    }

    /// <summary>Every email sent to <paramref name="to"/> so far, oldest first.</summary>
    public IReadOnlyList<SentEmail> SentTo(string to) =>
        _sent.Where(e => e.To.Equals(to, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Waits for an email to <paramref name="to"/> whose subject contains <paramref name="subjectPart"/>.
    /// Some emails are sent in the background after the response, so they may arrive a little later.
    /// </summary>
    public async Task<SentEmail> WaitForAsync(string to, string subjectPart)
    {
        SentEmail? found = null;
        await Eventually.TrueAsync(() =>
        {
            found = SentTo(to).LastOrDefault(e => e.Subject.Contains(subjectPart, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found != null);
        }, $"an email to {to} with '{subjectPart}' in the subject");
        return found!;
    }

    /// <summary>A handler for the backend's Resend client, backed by this fake.</summary>
    public HttpMessageHandler Handler() => new ResendHandler(this);

    private sealed class ResendHandler(FakeResend fake) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var to = body.GetProperty("to")[0].GetString()!;
            if (to.Contains(DownMarker))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);

            fake._sent.Enqueue(new SentEmail(to, body.GetProperty("subject").GetString()!, body.GetProperty("html").GetString()!));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

/// <summary>
/// Stand-in for TMDB. A title registered with <see cref="Add"/> is found by the search and returns
/// its details; any other title is not found. A title registered with <see cref="AddDown"/> makes
/// TMDB answer with a server error.
/// </summary>
public sealed class FakeTmdb
{
    /// <summary>What TMDB knows about a film or series.</summary>
    /// <param name="TitleEN">Title in English.</param>
    /// <param name="TitleES">Title in Spanish.</param>
    /// <param name="Overview">Synopsis.</param>
    /// <param name="PosterPath">Poster path, as TMDB returns it.</param>
    /// <param name="Runtime">Runtime in minutes (of an episode, for a series).</param>
    /// <param name="SeasonEpisodeCounts">Episodes per season, starting at season 1. Series only.</param>
    public sealed record Entry(
        string TitleEN, string TitleES, string? Overview = null, string? PosterPath = null,
        int? Runtime = null, int[]? SeasonEpisodeCounts = null);

    private readonly ConcurrentDictionary<string, (int Id, Entry Entry)> _byTitle = new();
    private readonly ConcurrentDictionary<int, Entry> _byId = new();
    private readonly ConcurrentDictionary<string, bool> _down = new();
    private int _nextId = 1000;

    /// <summary>Makes TMDB know <paramref name="entry"/>, searchable by either of its titles.</summary>
    public void Add(Entry entry)
    {
        var id = Interlocked.Increment(ref _nextId);
        _byId[id] = entry;
        _byTitle[entry.TitleEN] = (id, entry);
        _byTitle[entry.TitleES] = (id, entry);
    }

    /// <summary>Makes every TMDB request for <paramref name="title"/> fail with a server error.</summary>
    public void AddDown(string title) => _down[title] = true;

    /// <summary>A handler for the backend's TMDB client, backed by this fake.</summary>
    public HttpMessageHandler Handler() => new TmdbHandler(this);

    private sealed class TmdbHandler(FakeTmdb fake) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var query = HttpUtility.ParseQueryString(uri.Query);
            var segments = uri.AbsolutePath.Trim('/').Split('/'); // 3/search/movie or 3/movie/{id}
            var isMovie = segments.Contains("movie");

            if (segments[1] == "search")
            {
                var title = query["query"]!;
                if (fake._down.ContainsKey(title))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                if (!fake._byTitle.TryGetValue(title, out var hit))
                    return Json(new { results = Array.Empty<object>() });
                return Json(new { results = new[] { new { id = hit.Id, title = isMovie ? title : null, name = isMovie ? null : title } } });
            }

            if (!fake._byId.TryGetValue(int.Parse(segments[2]), out var e))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var spanish = query["language"] == "es-ES";
            var title2 = spanish ? e.TitleES : e.TitleEN;
            object Translation(string lang, string t) =>
                new { iso_639_1 = lang, data = new { title = isMovie ? t : null, name = isMovie ? null : t, overview = e.Overview } };

            return Json(new
            {
                title = isMovie ? title2 : null,
                name = isMovie ? null : title2,
                poster_path = e.PosterPath,
                overview = e.Overview,
                runtime = isMovie ? e.Runtime : null,
                episode_run_time = isMovie || e.Runtime is null ? null : new[] { e.Runtime.Value },
                seasons = e.SeasonEpisodeCounts?.Select((count, i) => new { season_number = i + 1, episode_count = count }),
                translations = new { translations = new[] { Translation("en", e.TitleEN), Translation("es", e.TitleES) } },
            });
        }

        private static Task<HttpResponseMessage> Json(object body) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
    }
}

/// <summary>
/// Stand-in for Open Library. A title registered with <see cref="Add"/> is found by the title
/// search; any other title is not found. It counts the searches per title, and a title registered
/// with <see cref="AddDown"/> makes Open Library answer with a server error.
/// </summary>
public sealed class FakeOpenLibrary
{
    /// <summary>What Open Library knows about a book.</summary>
    /// <param name="Title">Title.</param>
    /// <param name="Author">Author.</param>
    /// <param name="CoverId">Cover id, from which the cover URL is built.</param>
    /// <param name="Description">Synopsis of the work.</param>
    public sealed record Entry(string Title, string Author, int CoverId, string Description);

    private readonly ConcurrentDictionary<string, Entry> _byTitle = new();
    private readonly ConcurrentDictionary<string, bool> _down = new();
    private readonly ConcurrentDictionary<string, int> _searches = new();

    /// <summary>Makes Open Library know <paramref name="entry"/>.</summary>
    public void Add(Entry entry) => _byTitle[entry.Title] = entry;

    /// <summary>Makes every Open Library search for <paramref name="title"/> fail with a server error.</summary>
    public void AddDown(string title) => _down[title] = true;

    /// <summary>How many times the backend has searched for <paramref name="title"/>.</summary>
    public int SearchesFor(string title) => _searches.GetValueOrDefault(title);

    /// <summary>A handler for the backend's Open Library client, backed by this fake.</summary>
    public HttpMessageHandler Handler() => new OpenLibraryHandler(this);

    private sealed class OpenLibraryHandler(FakeOpenLibrary fake) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath == "/search.json")
            {
                var title = HttpUtility.ParseQueryString(uri.Query)["title"]!;
                fake._searches.AddOrUpdate(title, 1, (_, n) => n + 1);
                if (fake._down.ContainsKey(title))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                if (!fake._byTitle.TryGetValue(title, out var e))
                    return Json(new { docs = Array.Empty<object>() });
                var key = $"/works/{Uri.EscapeDataString(title)}";
                return Json(new { docs = new[] { new { key, title = e.Title, author_name = new[] { e.Author }, cover_i = e.CoverId } } });
            }

            if (uri.AbsolutePath.StartsWith("/works/"))
            {
                var title = Uri.UnescapeDataString(uri.AbsolutePath["/works/".Length..].Replace(".json", ""));
                if (fake._byTitle.TryGetValue(title, out var e))
                    return Json(new { description = e.Description });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> Json(object body) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
    }
}

/// <summary>
/// Stand-in for Google's id token validation. A token made with <see cref="Token"/> is valid and
/// carries the identity it was made with; anything else is rejected the way Google would.
/// </summary>
public sealed class FakeGoogleIdTokenValidator : GoogleIdTokenValidator
{
    private const string Prefix = "fake-google";

    /// <summary>A Google id token that this fake accepts, for the given Google account.</summary>
    /// <param name="googleId">Google account id.</param>
    /// <param name="email">Email of the Google account.</param>
    public static string Token(string googleId, string email) => $"{Prefix}|{googleId}|{email}";

    /// <inheritdoc />
    public override Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string clientId)
    {
        var parts = idToken.Split('|');
        if (parts.Length != 3 || parts[0] != Prefix)
            throw new InvalidJwtException("Not a token issued by the fake.");

        return Task.FromResult(new GoogleJsonWebSignature.Payload { Subject = parts[1], Email = parts[2] });
    }
}

/// <summary>Waits for something the backend does in the background, such as an email or metadata enrichment.</summary>
public static class Eventually
{
    /// <summary>Retries <paramref name="condition"/> until it holds, and fails the test after five seconds.</summary>
    /// <param name="condition">The check to repeat.</param>
    /// <param name="what">What is being waited for, for the failure message.</param>
    public static async Task TrueAsync(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new Xunit.Sdk.XunitException($"Timed out waiting for {what}.");
            await Task.Delay(50);
        }
    }
}
