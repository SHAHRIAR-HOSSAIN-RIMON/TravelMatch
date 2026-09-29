
namespace TravelMatch.API.DTOs.TripRequests;

public class TripRequestResultDto
{
    public bool Success { get; set; }

    public TripRequestError Error { get; set; } = TripRequestError.None;

    public string Message { get; set; } = string.Empty;

    public TripRequestResponseDto? Data { get; set; }
}

