namespace TravelMatch.API.DTOs.GuideApplications;

public enum GuideApplicationError
{
    None,
    NotFound,
    Unauthorized,
    AlreadyApplied,
    TripNotFound,
    TripNotOpen,
    TripNotAcceptingApplications,
    GuideNotFound,
    GuideNotVerified,
    InvalidPrice,
    MessageRequired,
    ServerError
}