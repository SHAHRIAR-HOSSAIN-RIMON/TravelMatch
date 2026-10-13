namespace TravelMatch.API.DTOs.ItineraryTracking;

public class ItineraryDayWithActivitiesDto
{
    public int Id { get; set; }

    public int ProposalId { get; set; }

    public int DayNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Activities { get; set; } = string.Empty;

    public string Accommodation { get; set; } = string.Empty;

    public string Meals { get; set; } = string.Empty;

    public List<ItineraryActivityDto> ActivitiesList { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}