namespace TravelMatch.API.DTOs.GuideVerification;

/// <summary>Guide profile and verification status available for administrative review.</summary>
public sealed record GuideVerificationSubmissionResponseDto(
    int GuideProfileId,
    int GuideUserId,
    string GuideName,
    string GuideEmail,
    string GuidePhoneNumber,
    string Bio,
    string ServiceArea,
    int ExperienceYears,
    string VerificationStatus,
    DateTimeOffset? SubmittedAt);
