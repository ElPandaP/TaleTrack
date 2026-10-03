namespace TaleTrackApp.Model;

/// <summary>
/// Kind of a <see cref="Media"/>. Persisted by name (see <see cref="Data.AppDbContext"/>), so the
/// names are part of the schema and the API contract.
/// </summary>
public enum MediaType
{
    /// <summary>A film.</summary>
    Movie,

    /// <summary>A TV series, tracked by season and episode.</summary>
    Series,

    /// <summary>A book.</summary>
    Book
}
