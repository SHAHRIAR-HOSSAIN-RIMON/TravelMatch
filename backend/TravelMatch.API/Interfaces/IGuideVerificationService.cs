using TravelMatch.API.DTOs.GuideVerification;

namespace TravelMatch.API.Interfaces;

public interface IGuideVerificationService
{
    /// <summary>
    /// Returns a paged list of guide profiles that currently have
    /// VerificationStatus == Pending. Admin-only operation; the service
    /// re-validates that <paramref name="adminUserId"/> belongs to an Admin.
    /// </summary>
    Task<GuideVerificationListResultDto> GetPendingGuidesAsync(
        int adminUserId,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Sets a guide's VerificationStatus to <c>Verified</c> or <c>Rejected</c>
    /// and notifies the guide. Caller must be an Admin.
    /// Only <c>Verified</c> and <c>Rejected</c> are accepted as the new status
    /// (transitioning back to <c>Pending</c> is not supported).
    /// </summary>
    Task<GuideVerificationResultDto> SetVerificationStatusAsync(
        int adminUserId,
        int guideUserId,
        string newStatus,
        string? note);
}
