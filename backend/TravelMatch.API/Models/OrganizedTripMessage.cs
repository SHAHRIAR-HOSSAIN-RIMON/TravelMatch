namespace TravelMatch.API.Models;

public class OrganizedTripMessage
{
    public int Id { get; set; }
    public int OrganizedTripId { get; set; }
    public OrganizedTrip OrganizedTrip { get; set; } = null!;
    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
