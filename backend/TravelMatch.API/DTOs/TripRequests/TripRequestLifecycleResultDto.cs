namespace TravelMatch.API.DTOs.TripRequests;

public class TripRequestLifecycleResultDto
{
    public bool Success { get; set; }

    public TripLifecycleError Error { get; set; }

    public string Message { get; set; } = string.Empty;

    public TripRequestLifecycleDto? Data { get; set; }
}