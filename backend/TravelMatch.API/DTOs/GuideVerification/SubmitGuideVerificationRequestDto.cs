using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.GuideVerification;

/// <summary>Guide profile information submitted for verification review.</summary>
public sealed record SubmitGuideVerificationRequestDto
{
    /// <summary>The guide's profile biography.</summary>
    [Required, StringLength(500)]
    public required string Bio { get; init; }

    /// <summary>The geographic area where the guide provides services.</summary>
    [Required, StringLength(100)]
    public required string ServiceArea { get; init; }

    /// <summary>The guide's years of professional experience.</summary>
    [Range(0, 60)]
    public required int ExperienceYears { get; init; }
}
