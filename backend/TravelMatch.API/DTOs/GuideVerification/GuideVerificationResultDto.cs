namespace TravelMatch.API.DTOs.GuideVerification;

/// <summary>Result of submitting or reviewing Guide verification information.</summary>
public sealed record GuideVerificationResultDto(
    bool Success,
    string Message,
    GuideVerificationError Error,
    GuideVerificationSubmissionResponseDto? Data);
