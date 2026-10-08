namespace TravelMatch.API.DTOs.OrganizedTrips;

public enum OrganizedTripError
{
    None,
    InvalidRequest,
    MissingRequiredField,
    StartDateInPast,
    EndDateBeforeStartDate,
    RegistrationDeadlineAfterStartDate,
    InvalidMaxParticipants,
    InvalidPricePerPerson,
    Unauthorized,
    OrganizerNotFound,
    OrganizedTripNotFound,
    InvalidStatusTransition
}
