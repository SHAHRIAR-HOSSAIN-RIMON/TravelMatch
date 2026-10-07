namespace TravelMatch.API.DTOs.Registrations;

public class RegistrationTouristDto
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;
}
