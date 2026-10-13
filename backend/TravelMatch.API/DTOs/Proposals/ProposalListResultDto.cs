namespace TravelMatch.API.DTOs.Proposals;

public class ProposalListResultDto
{
    public bool Success { get; set; }

    public ProposalError Error { get; set; } = ProposalError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<ProposalDto>? Data { get; set; }

    public int TotalCount { get; set; }

    public ProposalSort AppliedSort { get; set; }
}
