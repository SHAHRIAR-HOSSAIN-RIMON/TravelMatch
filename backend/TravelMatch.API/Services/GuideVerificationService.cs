using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.GuideVerification;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public sealed class GuideVerificationService : IGuideVerificationService
{
    private const int MaximumExperienceYears = 60;

    private readonly ApplicationDbContext _context;

    public GuideVerificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GuideVerificationResultDto> SubmitAsync(
        int guideUserId,
        SubmitGuideVerificationRequestDto request,
        CancellationToken cancellationToken)
    {
        var bio = request.Bio?.Trim();
        var serviceArea = request.ServiceArea?.Trim();
        if (string.IsNullOrWhiteSpace(bio) ||
            bio.Length > 500 ||
            string.IsNullOrWhiteSpace(serviceArea) ||
            serviceArea.Length > 100 ||
            request.ExperienceYears < 0 ||
            request.ExperienceYears > MaximumExperienceYears)
        {
            return Failure(
                GuideVerificationError.InvalidSubmission,
                "Bio, service area, and valid experience years are required.");
        }

        var user = await _context.Users
            .SingleOrDefaultAsync(
                candidate => candidate.Id == guideUserId &&
                    candidate.Role == UserRole.Guide &&
                    candidate.IsActive,
                cancellationToken);
        if (user is null)
        {
            return Failure(
                GuideVerificationError.GuideNotFound,
                "An active Guide account was not found.");
        }

        var profile = await _context.GuideProfiles
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == guideUserId,
                cancellationToken);
        if (profile is null)
        {
            return Failure(
                GuideVerificationError.GuideNotFound,
                "Guide profile was not found.");
        }

        if (profile.VerificationStatus == VerificationStatus.Verified)
        {
            return Failure(
                GuideVerificationError.AlreadyVerified,
                "A verified Guide cannot resubmit verification information.");
        }

        if (profile.VerificationStatus == VerificationStatus.Rejected)
        {
            return Failure(
                GuideVerificationError.NotPending,
                "Rejected verification submissions cannot be resubmitted under current requirements.");
        }

        if (profile.VerificationStatus == VerificationStatus.Pending &&
            profile.VerificationSubmittedAt is not null)
        {
            return Failure(
                GuideVerificationError.AlreadyPending,
                "A verification submission is already pending review.");
        }

        profile.Bio = bio;
        profile.ServiceArea = serviceArea;
        profile.ExperienceYears = request.ExperienceYears;
        profile.VerificationStatus = VerificationStatus.Pending;
        profile.VerificationSubmittedAt = DateTimeOffset.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await _context.Entry(profile).ReloadAsync(cancellationToken);
            return profile.VerificationStatus switch
            {
                VerificationStatus.Verified => Failure(
                    GuideVerificationError.AlreadyVerified,
                    "A verified Guide cannot resubmit verification information."),
                VerificationStatus.Pending when
                    profile.VerificationSubmittedAt is not null => Failure(
                        GuideVerificationError.AlreadyPending,
                        "A verification submission is already pending review."),
                _ => Failure(
                    GuideVerificationError.NotPending,
                    "The verification submission changed before it could be saved.")
            };
        }

        return new GuideVerificationResultDto(
            true,
            "Verification information submitted successfully.",
            GuideVerificationError.None,
            ToResponse(user, profile));
    }

    public async Task<IReadOnlyList<GuideVerificationSubmissionResponseDto>>
        GetPendingAsync(CancellationToken cancellationToken)
    {
        return await (
            from profile in _context.GuideProfiles.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on profile.UserId equals user.Id
            where profile.VerificationStatus == VerificationStatus.Pending &&
                profile.VerificationSubmittedAt != null
            orderby profile.VerificationSubmittedAt descending
            select new GuideVerificationSubmissionResponseDto(
                profile.Id,
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                profile.Bio,
                profile.ServiceArea,
                profile.ExperienceYears,
                profile.VerificationStatus.ToString(),
                profile.VerificationSubmittedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<GuideVerificationSubmissionResponseDto?> GetSubmissionAsync(
        int guideProfileId,
        CancellationToken cancellationToken)
    {
        return await (
            from profile in _context.GuideProfiles.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on profile.UserId equals user.Id
            where profile.Id == guideProfileId &&
                profile.VerificationSubmittedAt != null
            select new GuideVerificationSubmissionResponseDto(
                profile.Id,
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                profile.Bio,
                profile.ServiceArea,
                profile.ExperienceYears,
                profile.VerificationStatus.ToString(),
                profile.VerificationSubmittedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<GuideVerificationResultDto> ReviewAsync(
        int guideProfileId,
        VerificationStatus decision,
        CancellationToken cancellationToken)
    {
        if (decision is not VerificationStatus.Verified and
            not VerificationStatus.Rejected)
        {
            return Failure(
                GuideVerificationError.NotPending,
                "The requested verification decision is invalid.");
        }

        var profile = await _context.GuideProfiles
            .SingleOrDefaultAsync(
                candidate => candidate.Id == guideProfileId &&
                    candidate.VerificationSubmittedAt != null,
                cancellationToken);
        if (profile is null)
        {
            return Failure(
                GuideVerificationError.SubmissionNotFound,
                "Verification submission was not found.");
        }

        if (profile.VerificationStatus != VerificationStatus.Pending)
        {
            return Failure(
                GuideVerificationError.NotPending,
                "Only pending verification submissions can be reviewed.");
        }

        profile.VerificationStatus = decision;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(
                GuideVerificationError.NotPending,
                "The verification submission has already been reviewed.");
        }

        var user = await _context.Users
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.Id == profile.UserId,
                cancellationToken);

        return new GuideVerificationResultDto(
            true,
            $"Guide verification {decision.ToString().ToLowerInvariant()}.",
            GuideVerificationError.None,
            ToResponse(user, profile));
    }

    private static GuideVerificationResultDto Failure(
        GuideVerificationError error,
        string message)
    {
        return new GuideVerificationResultDto(false, message, error, null);
    }

    private static GuideVerificationSubmissionResponseDto ToResponse(
        User user,
        GuideProfile profile)
    {
        return new GuideVerificationSubmissionResponseDto(
            profile.Id,
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            profile.Bio,
            profile.ServiceArea,
            profile.ExperienceYears,
            profile.VerificationStatus.ToString(),
            profile.VerificationSubmittedAt);
    }
}
