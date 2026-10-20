using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.GuideVerification;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for the guide verification workflow:
///   - Admin can list pending guides
///   - Admin can approve a guide
///   - Admin can reject a guide (note required)
///   - Non-admin calls are rejected at the service layer
///   - Invalid guide IDs and bad status values are rejected
///   - Existing verification enforcement in GuideApplicationService is unchanged
/// </summary>
public class GuideVerificationTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static GuideVerificationService BuildService(ApplicationDbContext ctx) =>
        new(ctx, new NotificationService(
            ctx,
            NullLogger<NotificationService>.Instance,
            new NoopNotificationSender()));

    private static GuideApplicationService BuildGuideAppService(ApplicationDbContext ctx) =>
        new(ctx, NullLogger<GuideApplicationService>.Instance);

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private record AdminSeed(int UserId);
    private record GuideSeed(int UserId, int ProfileId);

    private static async Task<AdminSeed> SeedAdminAsync(
        ApplicationDbContext ctx, int userId = 900)
    {
        ctx.Users.Add(new User
        {
            Id = userId, FullName = "Admin", Email = $"admin{userId}@t.com",
            PhoneNumber = $"0179{userId:00000}", Role = UserRole.Admin, IsActive = true
        });
        await ctx.SaveChangesAsync();
        return new AdminSeed(userId);
    }

    private static async Task<GuideSeed> SeedGuideAsync(
        ApplicationDbContext ctx,
        int userId = 10,
        VerificationStatus status = VerificationStatus.Pending)
    {
        ctx.Users.Add(new User
        {
            Id = userId, FullName = "Guide One", Email = $"guide{userId}@t.com",
            PhoneNumber = $"0170{userId:000000}", Role = UserRole.Guide, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var profile = new GuideProfile
        {
            UserId             = userId,
            Bio                = "Experienced guide",
            ServiceArea        = "Dhaka",
            SupportedTripTypes = "Adventure",
            ExperienceYears    = 5,
            VerificationStatus = status
        };
        ctx.GuideProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return new GuideSeed(userId, profile.Id);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetPendingGuidesAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPending_ByAdmin_ReturnsPendingGuides()
    {
        await using var ctx = BuildContext(nameof(GetPending_ByAdmin_ReturnsPendingGuides));
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10, status: VerificationStatus.Pending);
        // Seed a verified guide — must NOT appear in pending list
        await SeedGuideAsync(ctx, userId: 11, status: VerificationStatus.Verified);
        var svc = BuildService(ctx);

        var result = await svc.GetPendingGuidesAsync(admin.UserId);

        Assert.True(result.Success);
        Assert.Single(result.Data);
        Assert.Equal(guide.UserId, result.Data[0].UserId);
        Assert.Equal("Pending", result.Data[0].VerificationStatus);
    }

    [Fact]
    public async Task GetPending_ByAdmin_EmptyList_WhenNoPendingGuides()
    {
        await using var ctx = BuildContext(nameof(GetPending_ByAdmin_EmptyList_WhenNoPendingGuides));
        var admin = await SeedAdminAsync(ctx);
        await SeedGuideAsync(ctx, userId: 10, status: VerificationStatus.Verified);
        var svc = BuildService(ctx);

        var result = await svc.GetPendingGuidesAsync(admin.UserId);

        Assert.True(result.Success);
        Assert.Empty(result.Data);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetPending_ByNonAdmin_ReturnsAdminOnly()
    {
        await using var ctx = BuildContext(nameof(GetPending_ByNonAdmin_ReturnsAdminOnly));
        var guide = await SeedGuideAsync(ctx, userId: 10);
        var svc = BuildService(ctx);

        // guide.UserId is not an admin
        var result = await svc.GetPendingGuidesAsync(guide.UserId);

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.AdminOnly, result.Error);
    }

    [Fact]
    public async Task GetPending_UnknownCaller_ReturnsAdminOnly()
    {
        await using var ctx = BuildContext(nameof(GetPending_UnknownCaller_ReturnsAdminOnly));
        var svc = BuildService(ctx);

        var result = await svc.GetPendingGuidesAsync(adminUserId: 9999);

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.AdminOnly, result.Error);
    }

    [Fact]
    public async Task GetPending_Pagination_ReturnsCorrectPage()
    {
        await using var ctx = BuildContext(nameof(GetPending_Pagination_ReturnsCorrectPage));
        var admin = await SeedAdminAsync(ctx);
        // Seed 5 pending guides
        for (int i = 1; i <= 5; i++)
            await SeedGuideAsync(ctx, userId: i, status: VerificationStatus.Pending);
        var svc = BuildService(ctx);

        var page1 = await svc.GetPendingGuidesAsync(admin.UserId, page: 1, pageSize: 3);
        var page2 = await svc.GetPendingGuidesAsync(admin.UserId, page: 2, pageSize: 3);

        Assert.True(page1.Success);
        Assert.Equal(3, page1.Data.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);

        Assert.True(page2.Success);
        Assert.Equal(2, page2.Data.Count);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SetVerificationStatusAsync — approval
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Approve_PendingGuide_ByAdmin_SetsVerified()
    {
        await using var ctx = BuildContext(nameof(Approve_PendingGuide_ByAdmin_SetsVerified));
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10, status: VerificationStatus.Pending);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, "Verified", note: null);

        Assert.True(result.Success);
        Assert.Equal("Verified", result.Data!.VerificationStatus);

        var saved = await ctx.GuideProfiles.FirstAsync(gp => gp.UserId == guide.UserId);
        Assert.Equal(VerificationStatus.Verified, saved.VerificationStatus);
    }

    [Fact]
    public async Task Approve_Guide_StatusIsCaseInsensitive()
    {
        await using var ctx = BuildContext(nameof(Approve_Guide_StatusIsCaseInsensitive));
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, "verified", note: null);

        Assert.True(result.Success);
        Assert.Equal("Verified", result.Data!.VerificationStatus);
    }

    [Fact]
    public async Task Approve_AlreadyVerifiedGuide_Succeeds()
    {
        // Idempotent — approving an already-verified guide is allowed
        await using var ctx = BuildContext(nameof(Approve_AlreadyVerifiedGuide_Succeeds));
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10, status: VerificationStatus.Verified);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, "Verified", note: null);

        Assert.True(result.Success);
        Assert.Equal("Verified", result.Data!.VerificationStatus);
    }

    [Fact]
    public async Task Approve_Guide_ResponseContainsProfileFields()
    {
        await using var ctx = BuildContext(nameof(Approve_Guide_ResponseContainsProfileFields));
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, "Verified", note: null);

        Assert.True(result.Success);
        Assert.Equal("Guide One", result.Data!.FullName);
        Assert.Equal("Dhaka", result.Data.ServiceArea);
        Assert.Equal(5, result.Data.ExperienceYears);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SetVerificationStatusAsync — rejection
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reject_PendingGuide_ByAdmin_SetsRejected()
    {
        await using var ctx = BuildContext(nameof(Reject_PendingGuide_ByAdmin_SetsRejected));
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10, status: VerificationStatus.Pending);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, "Rejected", note: "Incomplete documentation.");

        Assert.True(result.Success);
        Assert.Equal("Rejected", result.Data!.VerificationStatus);

        var saved = await ctx.GuideProfiles.FirstAsync(gp => gp.UserId == guide.UserId);
        Assert.Equal(VerificationStatus.Rejected, saved.VerificationStatus);
    }

    [Fact]
    public async Task Reject_WithoutNote_ReturnsInvalidInput()
    {
        await using var ctx = BuildContext(nameof(Reject_WithoutNote_ReturnsInvalidInput));
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, "Rejected", note: null);

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.InvalidInput, result.Error);

        // Profile must not have changed
        var saved = await ctx.GuideProfiles.FirstAsync(gp => gp.UserId == guide.UserId);
        Assert.Equal(VerificationStatus.Pending, saved.VerificationStatus);
    }

    [Fact]
    public async Task Reject_WithWhitespaceOnlyNote_ReturnsInvalidInput()
    {
        await using var ctx = BuildContext(nameof(Reject_WithWhitespaceOnlyNote_ReturnsInvalidInput));
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, "Rejected", note: "   ");

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.InvalidInput, result.Error);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SetVerificationStatusAsync — non-admin access
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(UserRole.Tourist)]
    [InlineData(UserRole.Guide)]
    [InlineData(UserRole.Organizer)]
    public async Task SetStatus_ByNonAdminRole_ReturnsAdminOnly(UserRole role)
    {
        await using var ctx = BuildContext(
            $"{nameof(SetStatus_ByNonAdminRole_ReturnsAdminOnly)}_{role}");

        // Seed a non-admin caller
        ctx.Users.Add(new User
        {
            Id = 50, FullName = "Caller", Email = "caller@t.com",
            PhoneNumber = "01700000050", Role = role, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var guide = await SeedGuideAsync(ctx, userId: 10);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            adminUserId: 50, guide.UserId, "Verified", note: null);

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.AdminOnly, result.Error);

        // Profile must not have changed
        var saved = await ctx.GuideProfiles.FirstAsync(gp => gp.UserId == guide.UserId);
        Assert.Equal(VerificationStatus.Pending, saved.VerificationStatus);
    }

    [Fact]
    public async Task SetStatus_UnknownAdminId_ReturnsAdminOnly()
    {
        await using var ctx = BuildContext(nameof(SetStatus_UnknownAdminId_ReturnsAdminOnly));
        var guide = await SeedGuideAsync(ctx, userId: 10);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            adminUserId: 9999, guide.UserId, "Verified", note: null);

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.AdminOnly, result.Error);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SetVerificationStatusAsync — invalid inputs
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SetStatus_InvalidGuideId_ReturnsInvalidInput()
    {
        await using var ctx = BuildContext(nameof(SetStatus_InvalidGuideId_ReturnsInvalidInput));
        var admin = await SeedAdminAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guideUserId: 0, "Verified", note: null);

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.InvalidInput, result.Error);
    }

    [Fact]
    public async Task SetStatus_GuideNotFound_ReturnsGuideNotFound()
    {
        await using var ctx = BuildContext(nameof(SetStatus_GuideNotFound_ReturnsGuideNotFound));
        var admin = await SeedAdminAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guideUserId: 999, "Verified", note: null);

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.GuideNotFound, result.Error);
    }

    [Fact]
    public async Task SetStatus_NonGuideUser_ReturnsGuideNotFound()
    {
        // Passing a Tourist user ID must not succeed
        await using var ctx = BuildContext(nameof(SetStatus_NonGuideUser_ReturnsGuideNotFound));
        var admin = await SeedAdminAsync(ctx);
        ctx.Users.Add(new User
        {
            Id = 20, FullName = "Tourist", Email = "tourist@t.com",
            PhoneNumber = "01700000020", Role = UserRole.Tourist, IsActive = true
        });
        await ctx.SaveChangesAsync();
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guideUserId: 20, "Verified", note: null);

        Assert.False(result.Success);
        Assert.Equal(GuideVerificationError.GuideNotFound, result.Error);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("pending")]
    [InlineData("invalid")]
    [InlineData("")]
    public async Task SetStatus_InvalidStatusValue_ReturnsInvalidStatus(string status)
    {
        await using var ctx = BuildContext(
            $"{nameof(SetStatus_InvalidStatusValue_ReturnsInvalidStatus)}_{status}");
        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10);
        var svc = BuildService(ctx);

        var result = await svc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, status, note: null);

        Assert.False(result.Success);
        Assert.Equal(
            status == "" ? GuideVerificationError.InvalidInput : GuideVerificationError.InvalidStatus,
            result.Error);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Existing verification enforcement — ensure it still works after approval
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExistingEnforcement_PendingGuide_CannotApplyForTrip()
    {
        // Regression: GuideApplicationService must still block a Pending guide
        await using var ctx = BuildContext(
            nameof(ExistingEnforcement_PendingGuide_CannotApplyForTrip));

        var guide = await SeedGuideAsync(ctx, userId: 10, status: VerificationStatus.Pending);

        // Seed an organizer + open organized trip for the guide to try to apply for
        ctx.Users.Add(new User
        {
            Id = 1, FullName = "Org", Email = "org@t.com",
            PhoneNumber = "01700000001", Role = UserRole.Organizer, IsActive = true
        });
        ctx.OrganizerProfiles.Add(new OrganizerProfile
        {
            Id = 10, UserId = 1, GroupOrganizationName = "TestOrg"
        });
        var trip = new OrganizedTrip
        {
            Id = 100, OrganizerId = 10, Title = "Trip A", Destination = "Sylhet",
            Description = "Test", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(37)),
            MaxParticipants = 10, PricePerPerson = 200m, Inclusions = "Food",
            RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            Status = OrganizedTripStatus.GuideSelection, CreatedAt = DateTime.UtcNow
        };
        ctx.OrganizedTrips.Add(trip);
        await ctx.SaveChangesAsync();

        var appSvc = BuildGuideAppService(ctx);
        var applyResult = await appSvc.ApplyAsync(
            guide.UserId, trip.Id,
            new TravelMatch.API.DTOs.GuideApplications.CreateGuideApplicationDto
            {
                ProposedPrice = 180m,
                Message = "I would like to guide this trip."
            });

        Assert.False(applyResult.Success);
        Assert.Equal(
            TravelMatch.API.DTOs.GuideApplications.GuideApplicationError.GuideNotVerified,
            applyResult.Error);
    }

    [Fact]
    public async Task ExistingEnforcement_AfterApproval_GuideCanApplyForTrip()
    {
        // After admin approves, the same guide must be allowed through
        await using var ctx = BuildContext(
            nameof(ExistingEnforcement_AfterApproval_GuideCanApplyForTrip));

        var admin = await SeedAdminAsync(ctx);
        var guide = await SeedGuideAsync(ctx, userId: 10, status: VerificationStatus.Pending);

        // Seed organizer + trip
        ctx.Users.Add(new User
        {
            Id = 1, FullName = "Org", Email = "org@t.com",
            PhoneNumber = "01700000001", Role = UserRole.Organizer, IsActive = true
        });
        ctx.OrganizerProfiles.Add(new OrganizerProfile
        {
            Id = 10, UserId = 1, GroupOrganizationName = "TestOrg"
        });
        var trip = new OrganizedTrip
        {
            Id = 100, OrganizerId = 10, Title = "Trip B", Destination = "Cox's Bazar",
            Description = "Test", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(37)),
            MaxParticipants = 10, PricePerPerson = 200m, Inclusions = "Food",
            RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            Status = OrganizedTripStatus.ApplicationOpen, CreatedAt = DateTime.UtcNow
        };
        ctx.OrganizedTrips.Add(trip);
        await ctx.SaveChangesAsync();

        // Admin approves the guide
        var verifSvc = BuildService(ctx);
        var approveResult = await verifSvc.SetVerificationStatusAsync(
            admin.UserId, guide.UserId, "Verified", note: null);
        Assert.True(approveResult.Success);

        // Guide should now be able to apply
        var appSvc = BuildGuideAppService(ctx);
        var applyResult = await appSvc.ApplyAsync(
            guide.UserId, trip.Id,
            new TravelMatch.API.DTOs.GuideApplications.CreateGuideApplicationDto
            {
                ProposedPrice = 180m,
                Message = "I would like to guide this trip."
            });

        Assert.True(applyResult.Success);
    }
}
