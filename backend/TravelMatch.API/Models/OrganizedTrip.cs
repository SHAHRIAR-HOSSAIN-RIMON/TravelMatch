namespace TravelMatch.API.Models;

public class OrganizedTrip
{
    public int Id { get; set; }

    public int OrganizerId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public OrganizedTripStatus Status { get; set; } = OrganizedTripStatus.ApplicationOpen;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public User Organizer { get; set; } = null!;

    public ICollection<GuideApplication> Applications { get; set; } = new List<GuideApplication>();
}
