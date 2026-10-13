namespace TravelMatch.API.DTOs.Itineraries;

public class ItineraryResultDto
{
    public bool Success { get; set; }

    public ItineraryError Error { get; set; } = ItineraryError.None;

    public string Message { get; set; } = string.Empty;

    public ItineraryDayDto? Data { get; set; }
}
