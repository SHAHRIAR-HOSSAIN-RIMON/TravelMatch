namespace TravelMatch.API.DTOs.Messaging;

public class TripMessageReadResultDto
{
    public bool Success { get; set; }

    public TripMessagingError Error { get; set; } = TripMessagingError.None;

    public string Message { get; set; } = string.Empty;

    public TripMessageDto? Data { get; set; }

    // True only when this call moved the message from unread to read,
    // which is the only case that should trigger a real-time receipt.
    public bool WasUnreadToRead { get; set; }
}
