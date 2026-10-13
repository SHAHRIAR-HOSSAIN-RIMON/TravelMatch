namespace TravelMatch.API.Models;

public class ItineraryDay
{
    public int Id { get; set; }

    public int ProposalId { get; set; }

    public Proposal Proposal { get; set; } = null!;

    public int DayNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Activities { get; set; } = string.Empty;

    public string Schedule { get; set; } = string.Empty;

    public string Accommodation { get; set; } = string.Empty;

    public string Meals { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<ItineraryActivity> ActivitiesList { get; set; }
        = new List<ItineraryActivity>();
}
