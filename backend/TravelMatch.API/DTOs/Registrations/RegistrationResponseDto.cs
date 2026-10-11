namespace TravelMatch.API.DTOs.Registrations;

public class RegistrationResponseDto
{
    public int Id { get; set; }

    public int OrganizedTripId { get; set; }

    public int NumberOfTravelers { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal TotalPrice { get; set; }

    public DateTime RegisteredAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public OrganizedTripSummaryDto OrganizedTrip { get; set; } = new();

    public RegistrationTouristDto Tourist { get; set; } = new();
}