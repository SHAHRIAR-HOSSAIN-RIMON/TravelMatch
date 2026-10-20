namespace TravelMatch.API.DTOs.GuideVerification;

/// <summary>Data returned for a single guide's verification record.</summary>
public class GuideVerificationResponseDto
{
    public int GuideProfileId { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>One of: Pending, Verified, Rejected.</summary>
    public string VerificationStatus { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    public string ServiceArea { get; set; } = string.Empty;

    public string SupportedTripTypes { get; set; } = string.Empty;

    public int ExperienceYears { get; set; }
}
