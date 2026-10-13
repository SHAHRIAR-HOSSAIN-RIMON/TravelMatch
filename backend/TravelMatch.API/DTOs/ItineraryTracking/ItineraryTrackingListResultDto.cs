namespace TravelMatch.API.DTOs.ItineraryTracking;

public class ItineraryTrackingListResultDto
{
    public bool Success { get; set; }

    public ItineraryTrackingError Error { get; set; } = ItineraryTrackingError.None;

    public string Message { get; set; } = string.Empty;

    public List<ItineraryDayWithActivitiesDto> Data { get; set; } = new();

    public int TotalCount { get; set; }
}