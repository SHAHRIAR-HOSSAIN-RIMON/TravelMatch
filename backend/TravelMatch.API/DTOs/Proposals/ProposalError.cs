namespace TravelMatch.API.DTOs.Proposals;

public enum ProposalError
{
    None,
    TripRequestNotFound,
    ProposalNotFound,
    Unauthorized,
    NoProposals,
    ServerError
}
