namespace TravelMatch.API.DTOs.OrganizedTrips;

/// <summary>
/// The public, tourist-facing view of a trip that is open for registration.
/// It deliberately omits organizer and internal user information.
/// </summary>
public class AvailableOrganizedTripDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int MaxParticipants { get; set; }

    public decimal PricePerPerson { get; set; }

    public string Inclusions { get; set; } = string.Empty;

    public string? Exclusions { get; set; }

    public DateOnly RegistrationDeadline { get; set; }

    public string Status { get; set; } = string.Empty;

    public int RemainingSpots { get; set; }
}
