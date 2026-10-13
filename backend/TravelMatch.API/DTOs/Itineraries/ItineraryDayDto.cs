namespace TravelMatch.API.DTOs.Itineraries;

public class ItineraryDayDto
{
    public int Id { get; set; }

    public int ProposalId { get; set; }

    public int DayNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Activities { get; set; } = string.Empty;

    public string? Schedule { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
