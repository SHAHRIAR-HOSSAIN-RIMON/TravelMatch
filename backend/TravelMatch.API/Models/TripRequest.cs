
namespace TravelMatch.API.Models;

public class TripRequest
{
    public int Id { get; set; }

    public int TouristId { get; set; }

    public string Destination { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int NumberOfTravelers { get; set; }

    public decimal Budget { get; set; }

    public string Description { get; set; } = string.Empty;

    public TripRequestStatus Status { get; set; } = TripRequestStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

