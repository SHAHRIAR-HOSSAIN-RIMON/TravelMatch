namespace TravelMatch.API.DTOs.Itineraries;

public class ItineraryDayListResultDto
{
    public bool Success { get; set; }

    public ItineraryError Error { get; set; } = ItineraryError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<ItineraryDayDto>? Data { get; set; }

    public int TotalCount { get; set; }
}
