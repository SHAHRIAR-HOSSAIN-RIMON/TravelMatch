namespace TravelMatch.API.DTOs.Notifications;

public class NotificationResultDto
{
    public bool Success { get; set; }

    public NotificationError Error { get; set; } = NotificationError.None;

    public string Message { get; set; } = string.Empty;

    public NotificationDto? Data { get; set; }
}

