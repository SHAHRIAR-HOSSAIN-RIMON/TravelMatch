namespace TravelMatch.API.DTOs.Profiles;

public class ProfileResultDto
{
    public bool Success { get; set; }

    public ProfileError Error { get; set; } = ProfileError.None;

    public string Message { get; set; } = string.Empty;

    public ProfileResponseDto? Data { get; set; }
}
