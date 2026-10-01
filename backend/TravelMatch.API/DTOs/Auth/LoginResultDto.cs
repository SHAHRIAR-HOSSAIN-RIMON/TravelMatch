namespace TravelMatch.API.DTOs.Auth;

public class LoginResultDto
{
    public bool Success { get; set; }

    public LoginError Error { get; set; } = LoginError.None;

    public string Message { get; set; } = string.Empty;

    public LoginResponseDto? Data { get; set; }
}