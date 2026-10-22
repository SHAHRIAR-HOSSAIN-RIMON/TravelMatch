using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Admin;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for GET /api/admin/dashboard. Service tests use EF Core InMemory directly;
/// framework authorization attributes are verified by reflection, consistent with
/// the existing admin-query tests. The test project has no HTTP-host harness.
/// </summary>
public class AdminDashboardTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static AdminService BuildService(ApplicationDbContext ctx) => new(ctx);

    private static AdminController BuildController(
        AdminService service, int? userId = 900, string? role = "Admin")
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        return new AdminController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static async Task SeedAdminAsync(ApplicationDbContext ctx, int userId = 900)
    {
        ctx.Users.Add(new User
        {
            Id = userId,
            FullName = "Admin",
            Email = $"admin{userId}@t.com",
            PhoneNumber = "01790000000",
            Role = UserRole.Admin,
            IsActive = true
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds every entity counted by the dashboard:
    /// 6 users (1 admin, 2 tourists, 2 guides, 1 organizer), 2 trip requests,
    /// 1 organized trip, 2 registrations, 1 proposal, 1 payment, 4 disputes
    /// (2 open) and 2 guide profiles (1 pending).
    /// </summary>
    private static async Task SeedDashboardDataAsync(ApplicationDbContext ctx)
    {
        ctx.Users.AddRange(
            new User { Id = 1, FullName = "Admin", Email = "admin@t.com", PhoneNumber = "01790000001", Role = UserRole.Admin, IsActive = true },
            new User { Id = 2, FullName = "Tourist One", Email = "tourist1@t.com", PhoneNumber = "01790000002", Role = UserRole.Tourist, IsActive = true },
            new User { Id = 3, FullName = "Guide One", Email = "guide1@t.com", PhoneNumber = "01790000003", Role = UserRole.Guide, IsActive = true },
            new User { Id = 4, FullName = "Organizer One", Email = "organizer1@t.com", PhoneNumber = "01790000004", Role = UserRole.Organizer, IsActive = true },
            new User { Id = 5, FullName = "Tourist Two", Email = "tourist2@t.com", PhoneNumber = "01790000005", Role = UserRole.Tourist, IsActive = false },
            new User { Id = 6, FullName = "Guide Two", Email = "guide2@t.com", PhoneNumber = "01790000006", Role = UserRole.Guide, IsActive = true });

        ctx.TouristProfiles.AddRange(
            new TouristProfile { Id = 1, UserId = 2 },
            new TouristProfile { Id = 2, UserId = 5 });

        ctx.GuideProfiles.AddRange(
            new GuideProfile { Id = 1, UserId = 3, VerificationStatus = VerificationStatus.Pending },
            new GuideProfile { Id = 2, UserId = 6, VerificationStatus = VerificationStatus.Verified });

        ctx.OrganizerProfiles.Add(new OrganizerProfile { Id = 1, UserId = 4 });

        ctx.TripRequests.AddRange(
            new TripRequest
            {
                Id = 1, TouristId = 2, Destination = "Cox's Bazar", TripType = "Beach",
                StartDate = new DateOnly(2026, 11, 1), EndDate = new DateOnly(2026, 11, 3),
                NumberOfTravelers = 2, Budget = 500m, BudgetMin = 400m, BudgetMax = 600m,
                TravelPreferences = "Relaxed", Status = TripRequestStatus.Pending
            },
            new TripRequest
            {
                Id = 2, TouristId = 5, Destination = "Sylhet", TripType = "Nature",
                StartDate = new DateOnly(2026, 12, 1), EndDate = new DateOnly(2026, 12, 3),
                NumberOfTravelers = 1, Budget = 300m, BudgetMin = 200m, BudgetMax = 400m,
                TravelPreferences = "Hiking", Status = TripRequestStatus.Completed
            });

        ctx.OrganizedTrips.Add(new OrganizedTrip
        {
            Id = 1, OrganizerId = 1, Title = "Sunrise Tour", Destination = "Bandarban",
            Description = "Three-day tour", StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 12), MaxParticipants = 10,
            PricePerPerson = 150m, Inclusions = "Transport", RegistrationDeadline = new DateOnly(2026, 11, 5),
            Status = OrganizedTripStatus.RegistrationOpen
        });

        ctx.Registrations.AddRange(
            new Registration { Id = 1, OrganizedTripId = 1, TouristId = 1, TotalPrice = 150m, Status = RegistrationStatus.Confirmed },
            new Registration { Id = 2, OrganizedTripId = 1, TouristId = 2, TotalPrice = 150m, Status = RegistrationStatus.Cancelled });

        ctx.Proposals.Add(new Proposal
        {
            Id = 1, TripRequestId = 1, GuideId = 3, ProposedPrice = 450m,
            Availability = "Nov 1-3", Inclusions = "Guide", Status = ProposalStatus.Submitted
        });

        ctx.Payments.Add(new Payment
        {
            Id = 1, TouristUserId = 2, TargetType = PaymentTargetType.Proposal, ProposalId = 1,
            Amount = 450m, Currency = "USD", Status = PaymentStatus.Held,
            TransactionReference = "TX-001"
        });

        ctx.Disputes.AddRange(
            new Dispute { Id = 1, ReportedByUserId = 2, TripType = DisputeRelatedTripType.TripRequest, TripId = 1, Reason = "R1", Description = "D1", Status = DisputeStatus.Open },
            new Dispute { Id = 2, ReportedByUserId = 5, TripType = DisputeRelatedTripType.OrganizedTrip, TripId = 1, Reason = "R2", Description = "D2", Status = DisputeStatus.Open },
            new Dispute { Id = 3, ReportedByUserId = 2, TripType = DisputeRelatedTripType.TripRequest, TripId = 1, Reason = "R3", Description = "D3", Status = DisputeStatus.Resolved },
            new Dispute { Id = 4, ReportedByUserId = 5, TripType = DisputeRelatedTripType.OrganizedTrip, TripId = 1, Reason = "R4", Description = "D4", Status = DisputeStatus.Closed });

        await ctx.SaveChangesAsync();
    }

    // ── Dashboard counts ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_ByAdmin_ReturnsCorrectAggregateCounts()
    {
        await using var ctx = BuildContext(nameof(GetDashboard_ByAdmin_ReturnsCorrectAggregateCounts));
        await SeedDashboardDataAsync(ctx);

        var result = await BuildService(ctx).GetDashboardAsync(1);

        Assert.True(result.Success);
        Assert.Equal(AdminError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal(6, result.Data!.TotalUsers);
        Assert.Equal(2, result.Data.TotalTourists);
        Assert.Equal(2, result.Data.TotalGuides);
        Assert.Equal(1, result.Data.TotalOrganizers);
        Assert.Equal(2, result.Data.TotalTripRequests);
        Assert.Equal(1, result.Data.TotalOrganizedTrips);
        Assert.Equal(2, result.Data.TotalRegistrations);
        Assert.Equal(1, result.Data.TotalProposals);
        Assert.Equal(1, result.Data.TotalPayments);
        Assert.Equal(2, result.Data.TotalOpenDisputes);
        Assert.Equal(1, result.Data.TotalPendingGuideVerifications);
    }

    [Fact]
    public async Task GetDashboard_EmptyDomainTables_ReturnsZeroCounts()
    {
        await using var ctx = BuildContext(nameof(GetDashboard_EmptyDomainTables_ReturnsZeroCounts));
        await SeedAdminAsync(ctx);

        var result = await BuildService(ctx).GetDashboardAsync(900);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.TotalUsers);
        Assert.Equal(0, result.Data.TotalTourists);
        Assert.Equal(0, result.Data.TotalGuides);
        Assert.Equal(0, result.Data.TotalOrganizers);
        Assert.Equal(0, result.Data.TotalTripRequests);
        Assert.Equal(0, result.Data.TotalOrganizedTrips);
        Assert.Equal(0, result.Data.TotalRegistrations);
        Assert.Equal(0, result.Data.TotalProposals);
        Assert.Equal(0, result.Data.TotalPayments);
        Assert.Equal(0, result.Data.TotalOpenDisputes);
        Assert.Equal(0, result.Data.TotalPendingGuideVerifications);
    }

    [Fact]
    public async Task GetDashboard_ByNonAdmin_ReturnsAdminOnly()
    {
        await using var ctx = BuildContext(nameof(GetDashboard_ByNonAdmin_ReturnsAdminOnly));
        ctx.Users.Add(new User
        {
            Id = 2, FullName = "Tourist", Email = "tourist@t.com",
            PhoneNumber = "01790000002", Role = UserRole.Tourist, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var result = await BuildService(ctx).GetDashboardAsync(2);

        Assert.False(result.Success);
        Assert.Equal(AdminError.AdminOnly, result.Error);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetDashboard_UnknownCaller_ReturnsAdminOnly()
    {
        await using var ctx = BuildContext(nameof(GetDashboard_UnknownCaller_ReturnsAdminOnly));

        var result = await BuildService(ctx).GetDashboardAsync(9999);

        Assert.False(result.Success);
        Assert.Equal(AdminError.AdminOnly, result.Error);
    }

    // ── Controller behavior ───────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_AdminCaller_Returns200WithCounts()
    {
        await using var ctx = BuildContext(nameof(GetDashboard_AdminCaller_Returns200WithCounts));
        await SeedDashboardDataAsync(ctx);
        var controller = BuildController(BuildService(ctx), userId: 1);

        var actionResult = await controller.GetDashboard();

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<AdminDashboardResultDto>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal(6, body.Data!.TotalUsers);
        Assert.Equal(2, body.Data.TotalOpenDisputes);
    }

    [Fact]
    public async Task GetDashboard_NonAdminCaller_Returns403()
    {
        await using var ctx = BuildContext(nameof(GetDashboard_NonAdminCaller_Returns403));
        ctx.Users.Add(new User
        {
            Id = 2, FullName = "Tourist", Email = "tourist@t.com",
            PhoneNumber = "01790000002", Role = UserRole.Tourist, IsActive = true
        });
        await ctx.SaveChangesAsync();
        var controller = BuildController(BuildService(ctx), userId: 2, role: "Tourist");

        var actionResult = await controller.GetDashboard();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var body = Assert.IsType<AdminDashboardResultDto>(objectResult.Value);
        Assert.Equal(AdminError.AdminOnly, body.Error);
    }

    [Fact]
    public async Task GetDashboard_MissingNameIdentifierClaim_Returns401()
    {
        await using var ctx = BuildContext(nameof(GetDashboard_MissingNameIdentifierClaim_Returns401));
        var controller = BuildController(BuildService(ctx), userId: null);

        var actionResult = await controller.GetDashboard();

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
    }

    // ── Endpoint shape / authorization attributes ─────────────────────────────

    [Fact]
    public void DashboardEndpoint_IsAdminOnly_ByAttributes()
    {
        var controllerType = typeof(AdminController);

        var controllerAuthorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(controllerAuthorize);
        Assert.Equal("Admin", controllerAuthorize!.Roles);

        var route = controllerType.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("api/admin", route!.Template);

        var method = controllerType.GetMethod(nameof(AdminController.GetDashboard));
        Assert.NotNull(method);

        var httpGet = method!.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGet);
        Assert.Equal("dashboard", httpGet!.Template);
    }
}
