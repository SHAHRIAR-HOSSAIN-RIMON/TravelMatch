namespace TravelMatch.API.DTOs.Auth;

public class RegisterResultDto
{
    public bool Success { get; set; }

    public RegisterError Error { get; set; } = RegisterError.None;

    public string Message { get; set; } = string.Empty;

    public RegisterResponseDto? Data { get; set; }
}