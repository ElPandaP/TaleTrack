namespace TaleTrackApp.Features.Library.GetLibrary;

public class GetLibraryResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public int Total { get; set; }
    public required List<LibraryItem> Data { get; set; }
}
