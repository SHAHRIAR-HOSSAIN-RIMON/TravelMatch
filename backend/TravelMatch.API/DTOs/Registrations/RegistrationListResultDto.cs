namespace TravelMatch.API.DTOs.Registrations;

public class RegistrationListResultDto
{
    public bool Success { get; set; }

    public RegistrationError Error { get; set; } = RegistrationError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<RegistrationResponseDto> Data { get; set; } = Array.Empty<RegistrationResponseDto>();

    public int TotalCount { get; set; }
}
