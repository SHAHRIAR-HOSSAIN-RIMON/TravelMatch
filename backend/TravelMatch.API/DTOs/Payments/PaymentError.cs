namespace TravelMatch.API.DTOs.Payments;

public enum PaymentError
{
    None,
    InvalidRequest,
    Unauthorized,
    TouristProfileNotFound,
    ProposalNotFound,
    TripRequestNotFound,
    RegistrationNotFound,
    OrganizedTripNotFound,
    InvalidProposalStatus,
    InvalidRegistrationStatus,
    AlreadyPaid,
    PaymentFailed,
    TripCapacityExceeded,
    RegistrationDeadlinePassed,
    TripNotAvailableForRegistration,
    ServerError
}
