namespace TravelMatch.API.DTOs.Proposals;

public enum AcceptProposalError
{
    None,
    TripRequestNotFound,
    ProposalNotFound,
    Unauthorized,
    AlreadyAccepted,
    ProposalNotAvailable,
    TripRequestAlreadyMatched,
    ServerError,
    TripRequestNotAvailable
}
