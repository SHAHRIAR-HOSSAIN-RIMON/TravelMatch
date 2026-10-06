namespace TravelMatch.API.DTOs.GuideApplications;

public class OpenOrganizedTripDetailResultDto
{
    public bool Success { get; set; }

    public GuideApplicationError Error { get; set; } = GuideApplicationError.None;

    public string Message { get; set; } = string.Empty;

    public OpenOrganizedTripDto? Data { get; set; }

    public string GuideVerificationStatus { get; set; } = string.Empty;

    public bool CanApply { get; set; }
}
