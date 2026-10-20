using TravelMatch.API.DTOs.Reviews;

namespace TravelMatch.API.Interfaces;

public interface IReviewService
{
    /// <summary>
    /// Submits a review for a completed trip request. The authenticated tourist's user ID
    /// is supplied by the controller and never taken from the request body. The trip must be
    /// completed, must belong to this tourist, and must have been served by the guide profile
    /// being reviewed. At most one review is allowed per trip request. On success the guide's
    /// AverageRating is recalculated from all persisted reviews.
    /// </summary>
    Task<ReviewResultDto> SubmitReviewAsync(int touristUserId, CreateReviewRequestDto request);

    /// <summary>
    /// Paged reviews for a guide profile (GuideProfile.Id), newest first with stable
    /// secondary ordering. GuideName is resolved through GuideProfile.UserId to User.FullName;
    /// tourist display names through the Review.Tourist relationship.
    /// </summary>
    Task<ReviewListResultDto> GetReviewsByGuideAsync(int guideProfileId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Guide profile ID, nullable average rating and review count computed from persisted
    /// reviews. Returns null when the guide profile does not exist; a guide without reviews
    /// keeps AverageRating null rather than zero.
    /// </summary>
    Task<GuideRatingDto?> GetGuideRatingAsync(int guideProfileId);
}
