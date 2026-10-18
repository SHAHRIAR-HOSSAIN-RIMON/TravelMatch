namespace TravelMatch.API.DTOs.Proposals;

public class SubmitProposalResultDto
{
    public bool Success { get; set; }

    public SubmitProposalError Error { get; set; } = SubmitProposalError.None;

    public string Message { get; set; } = string.Empty;

    public GuideProposalDto? Data { get; set; }
}
