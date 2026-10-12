namespace TravelMatch.API.DTOs.TripRequests;

public class TripRequestLifecycleDto
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

    public string Status { get; set; } = string.Empty;

    public int? MatchedGuideId { get; set; }

    public decimal? AcceptedPrice { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? PaymentCompletedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public IReadOnlyList<string> AllowedTransitions { get; set; } = Array.Empty<string>();
}