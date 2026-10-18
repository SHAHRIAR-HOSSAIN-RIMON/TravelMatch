namespace TravelMatch.API.DTOs.Disputes;

public enum DisputeError
{
    None,
    TripNotFound,
    NotAuthorized,
    InvalidInput,
    DisputeNotFound,
    DisputeAlreadyResolved,
    AdminOnly
}
