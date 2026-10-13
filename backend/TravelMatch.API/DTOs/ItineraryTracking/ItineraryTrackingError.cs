namespace TravelMatch.API.DTOs.ItineraryTracking;

public enum ItineraryTrackingError
{
    None,
    NotFound,
    Unauthorized,
    InvalidStatus,
    InvalidActivity,
    DuplicateOrderIndex,
    ActivityNotFound,
    DayNotFound,
    ProposalNotFound,
    ServerError
}