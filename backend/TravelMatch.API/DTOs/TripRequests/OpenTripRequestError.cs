namespace TravelMatch.API.DTOs.TripRequests;

public enum OpenTripRequestError
{
    None,
    GuideNotFound,
    TripRequestNotFound,
    TripRequestNotAvailable,
    InvalidFilter,
    ServerError
}