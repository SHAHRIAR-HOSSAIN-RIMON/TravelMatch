using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for GET /api/disputes and GET /api/disputes/{id}.
///
/// Service-layer tests use EF Core InMemory directly (same pattern as
/// DisputeResolutionTests). Controller-level authorization is verified through
/// reflection, consistent with the existing ResolveEndpoint_IsAdminOnlyByAttributes
/// test in the project.
/// </summary>
public class DisputeAdminQueryTests
{
    // ─── infrastructure ───────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static DisputeService BuildDisputeService(ApplicationDbContext ctx) =>
        new(ctx, new NotificationService(ctx, NullLogger<NotificationService>.Instance, new NoopNotificationSender()));

    /// <summary>Creates a minimal dispute with the given status and creation timestamp.</summary>
    private static Dispute MakeDispute(int id, DisputeStatus status, DateTime createdAt,
        int reporterUserId = 1, DisputeRelatedTripType tripType = DisputeRelatedTripType.OrganizedTrip,
        int tripId = 10)
        => new()
        {
            Id = id,
            ReportedByUserId = reporterUserId,
            TripType = tripType,
            TripId = tripId,
            Reason = $"Reason {id}",
            Description = $"Description {id}",
            Status = status,
            CreatedAt = createdAt
        };

    // ─── GetDisputesAsync — ordering ─────────────────────────────────────────

    [Fact]
    public async Task GetDisputes_NoFilter_ReturnsAllDisputesOldestFirst()
    {
        await using var ctx = BuildContext(nameof(GetDisputes_NoFilter_ReturnsAllDisputesOldestFirst));

        var base_ = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.Disputes.AddRange(
            MakeDispute(1, DisputeStatus.Resolved, base_.AddDays(2)),
            MakeDispute(2, DisputeStatus.Open,     base_.AddDays(0)),
            MakeDispute(3, DisputeStatus.Open,     base_.AddDays(1))
        );
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx).GetDisputesAsync(null);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Data.Count);

        // Oldest first
        Assert.Equal(2, result.Data[0].Id);
        Assert.Equal(3, result.Data[1].Id);
        Assert.Equal(1, result.Data[2].Id);
    }

    // ─── GetDisputesAsync — status filter ─────────────────────────────────────

    [Fact]
    public async Task GetDisputes_FilterByOpen_ReturnsOnlyOpenDisputes()
    {
        await using var ctx = BuildContext(nameof(GetDisputes_FilterByOpen_ReturnsOnlyOpenDisputes));

        var base_ = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.Disputes.AddRange(
            MakeDispute(1, DisputeStatus.Open,     base_),
            MakeDispute(2, DisputeStatus.Resolved, base_.AddDays(1)),
            MakeDispute(3, DisputeStatus.Open,     base_.AddDays(2))
        );
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx).GetDisputesAsync(DisputeStatus.Open);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Data, d => Assert.Equal(nameof(DisputeStatus.Open), d.Status));
    }

    [Fact]
    public async Task GetDisputes_FilterByResolved_ReturnsOnlyResolvedDisputes()
    {
        await using var ctx = BuildContext(nameof(GetDisputes_FilterByResolved_ReturnsOnlyResolvedDisputes));

        var base_ = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.Disputes.AddRange(
            MakeDispute(1, DisputeStatus.Open,     base_),
            MakeDispute(2, DisputeStatus.Resolved, base_.AddDays(1)),
            MakeDispute(3, DisputeStatus.Open,     base_.AddDays(2))
        );
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx).GetDisputesAsync(DisputeStatus.Resolved);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(nameof(DisputeStatus.Resolved), result.Data[0].Status);
    }

    // ─── GetDisputesAsync — empty result ─────────────────────────────────────

    [Fact]
    public async Task GetDisputes_EmptyDatabase_ReturnsSuccessWithEmptyCollection()
    {
        await using var ctx = BuildContext(nameof(GetDisputes_EmptyDatabase_ReturnsSuccessWithEmptyCollection));

        var result = await BuildDisputeService(ctx).GetDisputesAsync(null);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetDisputes_FilterMatchesNothing_ReturnsSuccessWithEmptyCollection()
    {
        await using var ctx = BuildContext(nameof(GetDisputes_FilterMatchesNothing_ReturnsSuccessWithEmptyCollection));

        // Only resolved disputes in the DB
        ctx.Disputes.Add(MakeDispute(1, DisputeStatus.Resolved,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx).GetDisputesAsync(DisputeStatus.Open);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Data);
    }

    // ─── GetDisputesAsync — response DTO fields ───────────────────────────────

    [Fact]
    public async Task GetDisputes_ResponseDto_ContainsExpectedFields()
    {
        await using var ctx = BuildContext(nameof(GetDisputes_ResponseDto_ContainsExpectedFields));

        var createdAt = new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);
        ctx.Disputes.Add(new Dispute
        {
            Id = 1,
            ReportedByUserId = 42,
            TripType = DisputeRelatedTripType.TripRequest,
            TripId = 99,
            Reason = "No show",
            Description = "Guide never arrived.",
            Status = DisputeStatus.Open,
            CreatedAt = createdAt
        });
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx).GetDisputesAsync(null);

        Assert.True(result.Success);
        var dto = Assert.Single(result.Data);
        Assert.Equal(1, dto.Id);
        Assert.Equal(42, dto.ReportedByUserId);
        Assert.Equal(nameof(DisputeRelatedTripType.TripRequest), dto.TripType);
        Assert.Equal(99, dto.TripId);
        Assert.Equal("No show", dto.Reason);
        Assert.Equal(nameof(DisputeStatus.Open), dto.Status);
        Assert.Equal(createdAt, dto.CreatedAt);
        // Description is present (needed for detail; present here too)
        Assert.Equal("Guide never arrived.", dto.Description);
    }

    // ─── GetDisputeByIdAsync — happy path ─────────────────────────────────────

    [Fact]
    public async Task GetDisputeById_ExistingDispute_ReturnsDispute()
    {
        await using var ctx = BuildContext(nameof(GetDisputeById_ExistingDispute_ReturnsDispute));

        var createdAt = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        ctx.Disputes.Add(new Dispute
        {
            Id = 7,
            ReportedByUserId = 5,
            TripType = DisputeRelatedTripType.OrganizedTrip,
            TripId = 20,
            Reason = "Overcharged",
            Description = "Extra fees added at checkout.",
            Status = DisputeStatus.Open,
            CreatedAt = createdAt
        });
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx).GetDisputeByIdAsync(7);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(7, result.Data!.Id);
        Assert.Equal(5, result.Data.ReportedByUserId);
        Assert.Equal(nameof(DisputeRelatedTripType.OrganizedTrip), result.Data.TripType);
        Assert.Equal(20, result.Data.TripId);
        Assert.Equal("Overcharged", result.Data.Reason);
        Assert.Equal("Extra fees added at checkout.", result.Data.Description);
        Assert.Equal(nameof(DisputeStatus.Open), result.Data.Status);
        Assert.Equal(createdAt, result.Data.CreatedAt);
    }

    [Fact]
    public async Task GetDisputeById_ResolvedDispute_IncludesResolutionFields()
    {
        await using var ctx = BuildContext(nameof(GetDisputeById_ResolvedDispute_IncludesResolutionFields));

        var resolvedAt = new DateTime(2026, 4, 5, 9, 0, 0, DateTimeKind.Utc);
        ctx.Disputes.Add(new Dispute
        {
            Id = 3,
            ReportedByUserId = 11,
            TripType = DisputeRelatedTripType.TripRequest,
            TripId = 55,
            Reason = "No service",
            Description = "No service provided.",
            Status = DisputeStatus.Resolved,
            CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            ResolutionNote = "Refund issued by admin.",
            ResolvedByUserId = 900,
            ResolvedAt = resolvedAt
        });
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx).GetDisputeByIdAsync(3);

        Assert.True(result.Success);
        Assert.Equal(nameof(DisputeStatus.Resolved), result.Data!.Status);
        Assert.Equal("Refund issued by admin.", result.Data.ResolutionNote);
        Assert.Equal(900, result.Data.ResolvedByUserId);
        Assert.Equal(resolvedAt, result.Data.ResolvedAt);
    }

    // ─── GetDisputeByIdAsync — 404 ───────────────────────────────────────────

    [Fact]
    public async Task GetDisputeById_Nonexistent_ReturnsDisputeNotFound()
    {
        await using var ctx = BuildContext(nameof(GetDisputeById_Nonexistent_ReturnsDisputeNotFound));

        var result = await BuildDisputeService(ctx).GetDisputeByIdAsync(424242);

        Assert.False(result.Success);
        Assert.Equal(DisputeError.DisputeNotFound, result.Error);
        Assert.Null(result.Data);
    }

    // ─── Controller — invalid status filter → 400 ────────────────────────────

    [Fact]
    public void GetDisputes_InvalidStatusString_ControllerReturnsBadRequest()
    {
        // Build the controller directly — no HTTP pipeline needed to test the
        // Enum.TryParse guard and the BadRequest return path.
        using var ctx = BuildContext(nameof(GetDisputes_InvalidStatusString_ControllerReturnsBadRequest));
        var svc = BuildDisputeService(ctx);
        var controller = new DisputesController(svc);

        // Invoke the action directly (sync-over-async is fine for this guard path
        // because the guard fires before any async DB call).
        var actionResult = controller.GetDisputes("NotAValidStatus").GetAwaiter().GetResult();

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var dto = Assert.IsType<DisputeListResultDto>(badRequest.Value);
        Assert.False(dto.Success);
        Assert.Equal(DisputeError.InvalidInput, dto.Error);
    }

    [Theory]
    [InlineData("open")]      // case-insensitive parsing must succeed — not a bad-request case
    [InlineData("OPEN")]
    [InlineData("Open")]
    public async Task GetDisputes_ValidStatusCaseVariants_DoNotReturnBadRequest(string status)
    {
        await using var ctx = BuildContext($"{nameof(GetDisputes_ValidStatusCaseVariants_DoNotReturnBadRequest)}_{status}");
        var svc = BuildDisputeService(ctx);
        var controller = new DisputesController(svc);

        var actionResult = await controller.GetDisputes(status);

        // Should be OkObjectResult, not BadRequest
        Assert.IsType<OkObjectResult>(actionResult);
    }

    // ─── Controller — GET /api/disputes/{id} returns 404 via controller ───────

    [Fact]
    public async Task GetDisputeById_Nonexistent_ControllerReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(GetDisputeById_Nonexistent_ControllerReturnsNotFound));
        var svc = BuildDisputeService(ctx);
        var controller = new DisputesController(svc);

        var actionResult = await controller.GetDisputeById(424242);

        Assert.IsType<NotFoundObjectResult>(actionResult);
        var body = Assert.IsType<DisputeResultDto>(((NotFoundObjectResult)actionResult).Value);
        Assert.False(body.Success);
        Assert.Equal(DisputeError.DisputeNotFound, body.Error);
    }

    [Fact]
    public async Task GetDisputeById_Existing_ControllerReturnsOk()
    {
        await using var ctx = BuildContext(nameof(GetDisputeById_Existing_ControllerReturnsOk));
        ctx.Disputes.Add(MakeDispute(5, DisputeStatus.Open,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await ctx.SaveChangesAsync();

        var svc = BuildDisputeService(ctx);
        var controller = new DisputesController(svc);

        var actionResult = await controller.GetDisputeById(5);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<DisputeResultDto>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal(5, body.Data!.Id);
    }

    // ─── Authorization — verified via reflection (same pattern as existing tests) ─

    [Fact]
    public void GetDisputesEndpoint_IsAdminOnly_ByAttributes()
    {
        var method = typeof(DisputesController).GetMethod(nameof(DisputesController.GetDisputes));
        Assert.NotNull(method);

        // Must be a GET endpoint
        var httpGet = method!.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGet);
        // Route is the bare collection route (no template argument)
        Assert.Null(httpGet!.Template);

        // Must require Admin role
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);

        // status is a query param, not a body or route param
        var statusParam = method.GetParameters().Single(p => p.Name == "status");
        Assert.NotNull(statusParam.GetCustomAttribute<FromQueryAttribute>());
    }

    [Fact]
    public void GetDisputeByIdEndpoint_IsAdminOnly_ByAttributes()
    {
        var method = typeof(DisputesController).GetMethod(nameof(DisputesController.GetDisputeById));
        Assert.NotNull(method);

        // Must be a GET endpoint with {disputeId:int} route template
        var httpGet = method!.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGet);
        Assert.Equal("{disputeId:int}", httpGet!.Template);

        // Must require Admin role
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);

        // disputeId comes from the route, not the body
        var idParam = method.GetParameters().Single(p => p.Name == "disputeId");
        Assert.NotNull(idParam.GetCustomAttribute<FromRouteAttribute>());
    }

    // ─── Full-cycle: raise disputes then query them ────────────────────────────

    [Fact]
    public async Task GetDisputes_AfterMultipleRaised_ListMatchesOrder()
    {
        await using var ctx = BuildContext(nameof(GetDisputes_AfterMultipleRaised_ListMatchesOrder));

        // Directly insert disputes with controlled timestamps to test ordering
        var t0 = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.Disputes.AddRange(
            MakeDispute(1, DisputeStatus.Open,     t0.AddHours(3)),
            MakeDispute(2, DisputeStatus.Open,     t0.AddHours(1)),
            MakeDispute(3, DisputeStatus.Resolved, t0.AddHours(2))
        );
        await ctx.SaveChangesAsync();

        var svc = BuildDisputeService(ctx);

        // All disputes: oldest first
        var all = await svc.GetDisputesAsync(null);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(2, all.Data[0].Id); // +1h
        Assert.Equal(3, all.Data[1].Id); // +2h
        Assert.Equal(1, all.Data[2].Id); // +3h

        // Open only
        var open = await svc.GetDisputesAsync(DisputeStatus.Open);
        Assert.Equal(2, open.TotalCount);
        Assert.Equal(2, open.Data[0].Id); // older open
        Assert.Equal(1, open.Data[1].Id); // newer open
    }
}
