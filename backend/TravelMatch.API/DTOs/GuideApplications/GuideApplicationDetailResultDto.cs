namespace TravelMatch.API.DTOs.GuideApplications;

public class GuideApplicationDetailResultDto
{
    public bool Success { get; set; }

    public GuideApplicationError Error { get; set; } = GuideApplicationError.None;

    public string Message { get; set; } = string.Empty;

    public GuideApplicationDto? Data { get; set; }
}
