namespace TravelMatch.API.DTOs.Messaging;

public class TripMessageResultDto
{
    public bool Success { get; set; }

    public TripMessagingError Error { get; set; } = TripMessagingError.None;

    public string Message { get; set; } = string.Empty;

    public TripMessageDto? Data { get; set; }
}

