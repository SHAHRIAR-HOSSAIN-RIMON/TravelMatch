namespace TravelMatch.API.Models;

public class Proposal
{
    public int Id { get; set; }

    public int GuideId { get; set; }

    public int TripRequestId { get; set; }

    public decimal ProposedPrice { get; set; }

    public string Message { get; set; } = string.Empty;

    public ProposalStatus Status { get; set; } = ProposalStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public User Guide { get; set; } = null!;

    public TripRequest TripRequest { get; set; } = null!;

    public ICollection<ItineraryDay> ItineraryDays { get; set; } = new List<ItineraryDay>();
}
