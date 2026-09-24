namespace TaleTrackApp.Features.Library.GetLibrary;

public class GetLibraryResponse
{
    /// <summary>Always `true`.</summary>
    public bool Success { get; set; }
    /// <summary>Rows in `data`.</summary>
    public int Count { get; set; }
    /// <summary>Rows matching the filters before `limit` was applied.</summary>
    public int Total { get; set; }
    /// <summary>Library rows.</summary>
    public required List<LibraryItem> Data { get; set; }
}
