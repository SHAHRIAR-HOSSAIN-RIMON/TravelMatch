namespace TravelMatch.API.DTOs.TripRequests;

public enum TripLifecycleError
{
    None,
    TripRequestNotFound,
    Unauthorized,
    InvalidStatusTransition,
    ProposalNotFound,
    ProposalNotSubmitted,
    PaymentNotCompleted,
    TripAlreadyStarted,
    TripAlreadyCompleted,
    TripAlreadyCancelled,
    NoValidProposals,
    AlreadyMatched
}