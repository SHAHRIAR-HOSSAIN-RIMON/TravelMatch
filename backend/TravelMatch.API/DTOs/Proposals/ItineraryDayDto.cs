namespace TravelMatch.API.DTOs.Proposals;

public class ItineraryDayDto
{
    public int DayNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Activities { get; set; } = string.Empty;

    public string Accommodation { get; set; } = string.Empty;

    public string Meals { get; set; } = string.Empty;
}
