namespace TravelMatch.API.Models;

public class Review
{
    public int Id { get; set; }

    // Tourist who wrote the review
    public int TouristId { get; set; }

    public User Tourist { get; set; } = null!;

    // Guide being reviewed
    public int GuideId { get; set; }

    public GuideProfile Guide { get; set; } = null!;

    // Associated completed trip
    public int TripRequestId { get; set; }

    public TripRequest TripRequest { get; set; } = null!;

    // Review details
    public int Rating { get; set; } // 1-5

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}