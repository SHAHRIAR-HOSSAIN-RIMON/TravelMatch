namespace TravelMatch.API.DTOs.Proposals;

public class ProposalDetailDto
{
    public int Id { get; set; }

    public GuideInfoDto Guide { get; set; } = new();

    public decimal Price { get; set; }

    public string Availability { get; set; } = string.Empty;

    public string Inclusions { get; set; } = string.Empty;

    public string Exclusions { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public IReadOnlyList<ItineraryDayDto> Itinerary { get; set; }
        = Array.Empty<ItineraryDayDto>();

    public decimal? MatchingScore { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsVerificationWarning { get; set; }

    public bool IsEligibleForAcceptance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
