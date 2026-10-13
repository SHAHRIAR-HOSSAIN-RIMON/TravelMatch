namespace TravelMatch.API.DTOs.Proposals;

public class GuideInfoDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public decimal? AverageRating { get; set; }

    public int ExperienceYears { get; set; }

    public string ServiceArea { get; set; } = string.Empty;

    public string VerificationStatus { get; set; } = string.Empty;

    public bool IsVerified { get; set; }
}
