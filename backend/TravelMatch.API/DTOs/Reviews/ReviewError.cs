namespace TravelMatch.API.DTOs.Reviews;

public enum ReviewError
{
    None,
    TripNotFound,
    TripNotCompleted,
    TouristGuideMismatch,
    SelfReviewNotAllowed,
    DuplicateReview,
    InvalidRating,
    ReviewNotFound,
    Unauthorized,
    GuideNotFound,
    // Required by ReviewService's catch-all handler so unexpected failures are reported
    // as failures (mirrors PaymentError.ServerError); without it an unhandled exception
    // would surface as Error=None.
    ServerError
}