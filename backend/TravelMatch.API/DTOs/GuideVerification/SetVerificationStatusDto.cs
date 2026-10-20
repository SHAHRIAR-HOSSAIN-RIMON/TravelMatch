using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.GuideVerification;

/// <summary>Request body for PATCH /api/admin/guide-verification/{guideUserId}.</summary>
public class SetVerificationStatusDto
{
    /// <summary>Must be "Verified" or "Rejected".</summary>
    [Required]
    public string Status { get; set; } = string.Empty;

    /// <summary>Optional note explaining the decision (required when rejecting).</summary>
    [MaxLength(1000)]
    public string? Note { get; set; }
}
