namespace TravelMatch.API.DTOs.Proposals;

public class UpdateProposalResultDto
{
    public bool Success { get; set; }

    public UpdateProposalError Error { get; set; } = UpdateProposalError.None;

    public string Message { get; set; } = string.Empty;

    public GuideProposalDto? Data { get; set; }
}
