namespace TravelMatch.API.DTOs.GuideVerification;

/// <summary>Result wrapper for a single-guide operation (approve / reject).</summary>
public class GuideVerificationResultDto
{
    public bool Success { get; set; }

    public GuideVerificationError Error { get; set; } = GuideVerificationError.None;

    public string Message { get; set; } = string.Empty;

    public GuideVerificationResponseDto? Data { get; set; }
}
