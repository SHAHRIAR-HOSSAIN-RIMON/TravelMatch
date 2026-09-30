namespace TravelMatch.API.Models;

public class Notification
{
    public int Id { get; set; }

    public int RecipientUserId { get; set; }

    public int TripRequestId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}