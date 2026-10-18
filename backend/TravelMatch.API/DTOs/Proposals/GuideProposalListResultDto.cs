namespace TravelMatch.API.DTOs.Proposals;

public class GuideProposalListResultDto
{
    public bool Success { get; set; }

    public ProposalError Error { get; set; } = ProposalError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<GuideProposalDto> Data { get; set; }
        = Array.Empty<GuideProposalDto>();

    public int TotalCount { get; set; }
}
