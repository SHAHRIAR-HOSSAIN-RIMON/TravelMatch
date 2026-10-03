namespace TravelMatch.API.DTOs.Notifications;

public class NotificationResponseDto
{
    public int Id { get; set; }

    public int TripRequestId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}