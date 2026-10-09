namespace TravelMatch.API.DTOs.ItineraryTracking;

public class ItineraryActivityDto
{
    public int Id { get; set; }

    public int ItineraryDayId { get; set; }

    public int ProposalId { get; set; }

    public int OrderIndex { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}