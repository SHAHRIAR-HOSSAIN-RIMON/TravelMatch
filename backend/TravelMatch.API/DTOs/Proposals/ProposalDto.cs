namespace TravelMatch.API.DTOs.Proposals;

public class ProposalDto
{
    public int Id { get; set; }

    public GuideInfoDto Guide { get; set; } = new();

    public decimal Price { get; set; }

    public string Availability { get; set; } = string.Empty;

    public string InclusionsSummary { get; set; } = string.Empty;

    public string ExclusionsSummary { get; set; } = string.Empty;

    public decimal? MatchingScore { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
