namespace TravelMatch.API.Models;

public enum RegistrationStatus
{
    Pending,
    Confirmed,
    Cancelled
}

public class Registration
{
    public int Id { get; set; }

    public int OrganizedTripId { get; set; }

    public int TouristId { get; set; }

    public int NumberOfTravelers { get; set; } = 1;

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

    public decimal TotalPrice { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }
}