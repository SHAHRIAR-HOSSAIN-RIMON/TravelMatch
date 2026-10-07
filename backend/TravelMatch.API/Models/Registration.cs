namespace TravelMatch.API.Models;

public class Registration
{
    public int Id { get; set; }

    public int OrganizedTripId { get; set; }

    public int TouristId { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
