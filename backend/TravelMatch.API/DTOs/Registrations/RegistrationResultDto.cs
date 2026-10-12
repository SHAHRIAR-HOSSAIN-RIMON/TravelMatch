namespace TravelMatch.API.DTOs.Registrations;

public class RegistrationResultDto
{
    public bool Success { get; set; }

    public RegistrationError Error { get; set; }

    public string Message { get; set; } = string.Empty;

    public RegistrationResponseDto? Data { get; set; }
}