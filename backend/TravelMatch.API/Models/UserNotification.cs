namespace TravelMatch.API.Models;

public class UserNotification
{
    public int Id { get; set; }

    public int RecipientUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }
}
