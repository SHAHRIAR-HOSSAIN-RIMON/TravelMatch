using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Profile;

/// <summary>
/// Editable profile fields. Properties left null are not changed; fields that do not
/// apply to the authenticated user's role are rejected by the service.
/// </summary>
public class UpdateProfileRequestDto
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(1000)]
    public string? Preferences { get; set; }

    // Max length depends on the caller's role: 500 for Guide, 2000 for Organizer.
    public string? Bio { get; set; }

    [MaxLength(100)]
    public string? ServiceArea { get; set; }

    [Range(0, int.MaxValue)]
    public int? ExperienceYears { get; set; }

    [MaxLength(500)]
    public string? SupportedTripTypes { get; set; }

    [MaxLength(500)]
    public string? PhotoUrl { get; set; }

    [MaxLength(253)]
    public string? GroupOrganizationName { get; set; }
}
