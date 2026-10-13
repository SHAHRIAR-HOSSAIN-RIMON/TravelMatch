namespace TravelMatch.API.Models;

public class ItineraryActivity
{
    public int Id { get; set; }

    public int ItineraryDayId { get; set; }

    public ItineraryDay ItineraryDay { get; set; } = null!;

    public int OrderIndex { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ActivityStatus Status { get; set; } = ActivityStatus.Upcoming;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}