namespace TravelMatch.API.DTOs.ItineraryTracking;

public class ItineraryTrackingResultDto
{
    public bool Success { get; set; }

    public ItineraryTrackingError Error { get; set; } = ItineraryTrackingError.None;

    public string Message { get; set; } = string.Empty;

    public ItineraryActivityDto? Data { get; set; }
}