namespace TravelMatch.API.DTOs.Proposals;

public class ProposalDetailResultDto
{
    public bool Success { get; set; }

    public ProposalError Error { get; set; } = ProposalError.None;

    public string Message { get; set; } = string.Empty;

    public ProposalDetailDto? Data { get; set; }
}
