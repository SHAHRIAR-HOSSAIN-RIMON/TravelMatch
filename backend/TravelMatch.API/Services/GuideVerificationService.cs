using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.GuideVerification;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class GuideVerificationService : IGuideVerificationService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public GuideVerificationService(
        ApplicationDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    // ── List pending guides ───────────────────────────────────────────────────

    public async Task<GuideVerificationListResultDto> GetPendingGuidesAsync(
        int adminUserId,
        int page = 1,
        int pageSize = 20)
    {
        // Service-layer admin check mirrors the pattern in DisputeService.ResolveDisputeAsync
        var admin = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == adminUserId);

        if (admin is null || admin.Role != UserRole.Admin)
            return new GuideVerificationListResultDto
            {
                Success = false,
                Error   = GuideVerificationError.AdminOnly,
                Message = "Only an administrator may view pending guide verifications."
            };

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Join GuideProfiles → Users, filter to Pending, project to DTO
        var query = _context.GuideProfiles
            .AsNoTracking()
            .Where(gp => gp.VerificationStatus == VerificationStatus.Pending)
            .Join(
                _context.Users.AsNoTracking().Where(u => u.IsActive),
                gp => gp.UserId,
                u  => u.Id,
                (gp, u) => new GuideVerificationResponseDto
                {
                    GuideProfileId      = gp.Id,
                    UserId              = u.Id,
                    FullName            = u.FullName,
                    Email               = u.Email,
                    VerificationStatus  = gp.VerificationStatus.ToString(),
                    Bio                 = gp.Bio,
                    ServiceArea         = gp.ServiceArea,
                    SupportedTripTypes  = gp.SupportedTripTypes,
                    ExperienceYears     = gp.ExperienceYears
                });

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderBy(dto => dto.GuideProfileId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new GuideVerificationListResultDto
        {
            Success    = true,
            Message    = "Pending guide verifications retrieved successfully.",
            Data       = items,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    // ── Approve / reject a guide ──────────────────────────────────────────────

    public async Task<GuideVerificationResultDto> SetVerificationStatusAsync(
        int adminUserId,
        int guideUserId,
        string newStatus,
        string? note)
    {
        // 1. Validate inputs
        if (guideUserId <= 0)
            return Failure(GuideVerificationError.InvalidInput,
                "Invalid guide user identifier.");

        if (string.IsNullOrWhiteSpace(newStatus))
            return Failure(GuideVerificationError.InvalidInput,
                "A verification status is required.");

        if (!Enum.TryParse<VerificationStatus>(newStatus.Trim(), ignoreCase: true, out var parsedStatus)
            || parsedStatus == VerificationStatus.Pending)
            return Failure(GuideVerificationError.InvalidStatus,
                "Status must be 'Verified' or 'Rejected'.");

        if (parsedStatus == VerificationStatus.Rejected
            && string.IsNullOrWhiteSpace(note))
            return Failure(GuideVerificationError.InvalidInput,
                "A note is required when rejecting a guide.");

        var trimmedNote = note?.Trim();
        if (trimmedNote?.Length > 1000)
            return Failure(GuideVerificationError.InvalidInput,
                "Note must be 1000 characters or fewer.");

        // 2. Verify caller is admin (double enforcement — JWT + DB)
        var admin = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == adminUserId);

        if (admin is null || admin.Role != UserRole.Admin)
            return Failure(GuideVerificationError.AdminOnly,
                "Only an administrator may set guide verification status.");

        // 3. Locate the guide user
        var guideUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == guideUserId && u.Role == UserRole.Guide);

        if (guideUser is null)
            return Failure(GuideVerificationError.GuideNotFound,
                "Guide not found.");

        // 4. Locate the guide profile
        var guideProfile = await _context.GuideProfiles
            .FirstOrDefaultAsync(gp => gp.UserId == guideUserId);

        if (guideProfile is null)
            return Failure(GuideVerificationError.GuideNotFound,
                "Guide profile not found.");

        // 5. Apply the status change
        guideProfile.VerificationStatus = parsedStatus;
        await _context.SaveChangesAsync();

        // 6. Notify the guide — fire-and-forget; failure must not abort the operation
        try
        {
            var (title, message) = parsedStatus == VerificationStatus.Verified
                ? ("Verification approved",
                   "Your guide profile has been verified. You can now apply for trips and submit proposals.")
                : ("Verification rejected",
                   string.IsNullOrWhiteSpace(trimmedNote)
                       ? "Your guide verification request has been rejected."
                       : $"Your guide verification request has been rejected. Note: {trimmedNote}");

            await _notificationService.CreateNotificationAsync(guideUserId, title, message);
        }
        catch
        {
            // Notification failure does not roll back the status change
        }

        return new GuideVerificationResultDto
        {
            Success = true,
            Message = parsedStatus == VerificationStatus.Verified
                ? "Guide verified successfully."
                : "Guide verification rejected.",
            Data = new GuideVerificationResponseDto
            {
                GuideProfileId     = guideProfile.Id,
                UserId             = guideUser.Id,
                FullName           = guideUser.FullName,
                Email              = guideUser.Email,
                VerificationStatus = guideProfile.VerificationStatus.ToString(),
                Bio                = guideProfile.Bio,
                ServiceArea        = guideProfile.ServiceArea,
                SupportedTripTypes = guideProfile.SupportedTripTypes,
                ExperienceYears    = guideProfile.ExperienceYears
            }
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GuideVerificationResultDto Failure(
        GuideVerificationError error,
        string message) =>
        new() { Success = false, Error = error, Message = message };
}
