namespace TravelMatch.API.Models;

public class GuideProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Bio { get; set; } = string.Empty;

    public string ServiceArea { get; set; } = string.Empty;

    public int ExperienceYears { get; set; }

    public decimal? AverageRating { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
}