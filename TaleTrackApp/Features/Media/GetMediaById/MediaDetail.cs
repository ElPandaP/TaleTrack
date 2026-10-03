namespace TaleTrackApp.Features.Media.GetMediaById;

/// <summary>What a media detail page needs: the media, all its reviews and the viewer's own tracking.</summary>
/// <param name="Media">The media.</param>
/// <param name="Reviews">Every user's review of it.</param>
/// <param name="MyReview">The viewer's review, if they wrote one.</param>
/// <param name="MyTracking">The viewer's tracking row, if they track it.</param>
/// <param name="MyProgress">The viewer's progress as shown to them (derived from the episode for a series).</param>
public record MediaDetail(
    Model.Media Media,
    List<Model.Review> Reviews,
    Model.Review? MyReview,
    Model.TrackingEvent? MyTracking,
    int? MyProgress);
