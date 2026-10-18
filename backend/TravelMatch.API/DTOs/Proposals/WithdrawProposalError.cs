namespace TravelMatch.API.DTOs.Proposals;

public enum WithdrawProposalError
{
    None,
    ProposalNotFound,
    Unauthorized,
    ProposalAlreadyWithdrawn,
    ProposalAlreadyAccepted,
    ProposalAlreadyRejected,
    ServerError
}
