namespace TaleTrackApp.Features.Media.GetMediaById;

/// <summary>What a media detail page needs: the media, all its reviews and the viewer's own tracking.</summary>
public record MediaDetail(
    Model.Media Media,
    List<Model.Review> Reviews,
    Model.Review? MyReview,
    Model.TrackingEvent? MyTracking,
    int? MyProgress);
