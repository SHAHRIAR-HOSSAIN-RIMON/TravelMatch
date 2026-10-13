namespace TravelMatch.API.Models;

public class TripRequest
{
    public int Id { get; set; }

    public int TouristId { get; set; }

    public string Destination { get; set; } = string.Empty;

    public string TripType { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int NumberOfTravelers { get; set; }

    public decimal Budget { get; set; }

    public decimal BudgetMin { get; set; }

    public decimal BudgetMax { get; set; }

    public string Description { get; set; } = string.Empty;

    public string TravelPreferences { get; set; } = string.Empty;

    public TripRequestStatus Status { get; set; } = TripRequestStatus.Pending;

    public int? MatchedGuideId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public decimal? AcceptedPrice { get; set; }

    public DateTime? PaymentCompletedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }
}