namespace TravelMatch.API.Models;

public class Proposal
{
    public int Id { get; set; }

    public int TripRequestId { get; set; }

    public TripRequest TripRequest { get; set; } = null!;

    public int GuideId { get; set; }

    public User Guide { get; set; } = null!;

    public decimal Price { get; set; }

    public string Availability { get; set; } = string.Empty;

    public string Inclusions { get; set; } = string.Empty;

    public string Exclusions { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public decimal? MatchingScore { get; set; }

    public ProposalStatus Status { get; set; } = ProposalStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<ItineraryDay> ItineraryDays { get; set; }
        = new List<ItineraryDay>();
}
