namespace TravelMatch.API.DTOs.GuideApplications;

public class SelectGuideApplicationResultDto
{
    public bool Success { get; set; }

    public GuideApplicationError Error { get; set; } = GuideApplicationError.None;

    public string Message { get; set; } = string.Empty;

    public int? ApplicationId { get; set; }

    public int? GuideId { get; set; }

    public string? GuideName { get; set; }

    public int? TripId { get; set; }

    public string? TripStatus { get; set; }
}