
namespace TravelMatch.API.DTOs.TripRequests;

public enum TripRequestError
{
    None,
    InvalidDestination,
    InvalidTripType,
    InvalidStartDate,
    InvalidEndDate,
    StartDateInPast,
    EndDateBeforeStartDate,
    InvalidNumberOfTravelers,
    InvalidBudget,
    Unauthorized,
    TouristNotFound,
    TripRequestNotFound,
    TripRequestNotOpen,
    ServerError
}
