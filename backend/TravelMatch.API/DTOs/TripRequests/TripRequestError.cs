
namespace TravelMatch.API.DTOs.TripRequests;

public enum TripRequestError
{
    None,
    InvalidStartDate,
    InvalidEndDate,
    StartDateInPast,
    EndDateBeforeStartDate,
    InvalidNumberOfTravelers,
    InvalidBudget,
    Unauthorized,
    TouristNotFound
}
