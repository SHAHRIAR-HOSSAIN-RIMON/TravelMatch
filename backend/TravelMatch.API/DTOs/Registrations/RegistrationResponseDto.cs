namespace TravelMatch.API.DTOs.Registrations;

public class RegistrationResponseDto
{
    public int Id { get; set; }

    public int OrganizedTripId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public RegistrationTouristDto Tourist { get; set; } = new();
}
