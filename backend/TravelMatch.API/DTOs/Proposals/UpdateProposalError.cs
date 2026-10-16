namespace TravelMatch.API.DTOs.Proposals;

public enum UpdateProposalError
{
    None,
    ProposalNotFound,
    Unauthorized,
    ProposalNotEditable,
    ValidationError,
    ServerError
}
