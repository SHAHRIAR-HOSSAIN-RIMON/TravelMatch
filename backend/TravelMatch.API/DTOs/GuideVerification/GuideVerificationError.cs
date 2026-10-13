namespace TravelMatch.API.DTOs.GuideVerification;

/// <summary>Describes the reason a Guide verification operation failed.</summary>
public enum GuideVerificationError
{
    None,
    GuideNotFound,
    SubmissionNotFound,
    InvalidSubmission,
    AlreadyPending,
    AlreadyVerified,
    NotPending
}
