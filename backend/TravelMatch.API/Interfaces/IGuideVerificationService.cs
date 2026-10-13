using TravelMatch.API.DTOs.GuideVerification;
using TravelMatch.API.Models;

namespace TravelMatch.API.Interfaces;

public interface IGuideVerificationService
{
    Task<GuideVerificationResultDto> SubmitAsync(
        int guideUserId,
        SubmitGuideVerificationRequestDto request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GuideVerificationSubmissionResponseDto>> GetPendingAsync(
        CancellationToken cancellationToken);

    Task<GuideVerificationSubmissionResponseDto?> GetSubmissionAsync(
        int guideProfileId,
        CancellationToken cancellationToken);

    Task<GuideVerificationResultDto> ReviewAsync(
        int guideProfileId,
        VerificationStatus decision,
        CancellationToken cancellationToken);
}
