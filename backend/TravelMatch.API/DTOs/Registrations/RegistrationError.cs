namespace TravelMatch.API.DTOs.Registrations;

public enum RegistrationError
{
    None,
    OrganizerNotFound,
    OrganizedTripNotFound,
    Unauthorized,
    TripFull,
    DeadlinePassed,
    ExceedsCapacity,
    DuplicateRegistration,
    InvalidNumberOfTravelers,
    TripNotOpenForRegistration,
    TripCancelled,
    PaymentFailed,
    RegistrationNotFound
}