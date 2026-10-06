namespace TravelMatch.API.DTOs.Proposals;

public class AcceptProposalResultDto
{
    public bool Success { get; set; }

    public int Error { get; set; } = AcceptProposalError.None;

    public string Message { get; set; } = string.Empty;

    public int? AcceptedProposalId { get; set; }

    public int? TripRequestId { get; set; }

    public int? GuideId { get; set; }
}
