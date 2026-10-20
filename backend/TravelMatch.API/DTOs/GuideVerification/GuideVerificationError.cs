namespace TravelMatch.API.DTOs.GuideVerification;

public enum GuideVerificationError
{
    None,
    AdminOnly,
    GuideNotFound,
    InvalidStatus,
    InvalidInput
}
