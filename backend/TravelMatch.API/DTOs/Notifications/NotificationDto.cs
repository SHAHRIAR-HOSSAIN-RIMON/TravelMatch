namespace TravelMatch.API.DTOs.Notifications;

public class NotificationDto
{
    public int Id { get; set; }

    public int RecipientUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }
}

