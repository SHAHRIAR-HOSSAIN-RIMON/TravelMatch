namespace TravelMatch.API.DTOs.Proposals;

public enum ProposalError
{
    None,
    GuideNotVerified,
    TripRequestNotFound,
    TripRequestNotOpen,
    DuplicateProposal
}