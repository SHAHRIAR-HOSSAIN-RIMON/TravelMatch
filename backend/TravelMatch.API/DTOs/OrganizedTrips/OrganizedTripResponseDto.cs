namespace TravelMatch.API.DTOs.OrganizedTrips;

public class OrganizedTripResponseDto
{
    public int Id { get; set; }

    public int OrganizerId { get; set; }

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

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
