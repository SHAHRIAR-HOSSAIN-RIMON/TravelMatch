namespace TravelMatch.API.DTOs.Profiles;

public class GuideProfileDto
{
    public int Id { get; set; }

    public string Bio { get; set; } = string.Empty;

    public string ServiceArea { get; set; } = string.Empty;

    public int ExperienceYears { get; set; }

    public decimal? AverageRating { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;
}
