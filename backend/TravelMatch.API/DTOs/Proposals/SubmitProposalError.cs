namespace TravelMatch.API.DTOs.Proposals;

public enum SubmitProposalError
{
    None,
    ProposalNotFound,
    TripRequestNotFound,
    Unauthorized,
    GuideNotFound,
    GuideNotVerified,
    ProposalNotInDraftState,
    TripRequestNotAcceptingProposals,
    DuplicateProposal,
    ValidationError,
    ServerError
}
