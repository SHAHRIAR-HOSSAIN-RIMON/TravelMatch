namespace TravelMatch.API.DTOs.Proposals;

public class WithdrawProposalResultDto
{
    public bool Success { get; set; }

    public WithdrawProposalError Error { get; set; } = WithdrawProposalError.None;

    public string Message { get; set; } = string.Empty;
}
