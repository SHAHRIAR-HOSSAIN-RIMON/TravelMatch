namespace TravelMatch.API.Models;

public class GuideApplication
{
    public int Id { get; set; }

    public int GuideId { get; set; }

    public int TripId { get; set; }

    public decimal ProposedPrice { get; set; }

    public string Message { get; set; } = string.Empty;

    public GuideApplicationStatus Status { get; set; } = GuideApplicationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public User Guide { get; set; } = null!;

    public OrganizedTrip Trip { get; set; } = null!;
}
