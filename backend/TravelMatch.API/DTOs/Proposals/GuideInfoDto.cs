namespace TravelMatch.API.DTOs.Proposals;

public class GuideInfoDto
{
    public int Id { get; set; }

    // Id remains the User.Id for existing clients; reviews require this profile ID.
    public int? GuideProfileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public decimal? AverageRating { get; set; }

    public int ExperienceYears { get; set; }

    public string ServiceArea { get; set; } = string.Empty;

    public string VerificationStatus { get; set; } = string.Empty;

    public bool IsVerified { get; set; }
}
