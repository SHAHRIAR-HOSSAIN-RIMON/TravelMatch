using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tasks 2 and 3: administrator-only dispute resolution, and the dispute rules that
/// escrow release and refunds must honour — an open dispute blocks, a resolved
/// dispute must not, and the block must be scoped to the disputed trip only.
/// Uses EF Core InMemory — no real database, no real money.
/// </summary>
public class DisputeResolutionTests
{
    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static DisputeService BuildDisputeService(ApplicationDbContext ctx) =>
        new(ctx, new NotificationService(ctx, NullLogger<NotificationService>.Instance, new NoopNotificationSender()));

    private static PaymentService BuildPaymentService(ApplicationDbContext ctx) =>
        new PaymentService(ctx, new SandboxPaymentGateway(), NullLogger<PaymentService>.Instance, new NotificationService(ctx, NullLogger<NotificationService>.Instance, new NoopNotificationSender()));

    private static DateOnly FutureDate(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days));

    private static DateOnly PastDate(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

    private record Admin(int UserId);

    private static async Task<Admin> SeedAdminAsync(ApplicationDbContext ctx, int userId = 900)
    {
        ctx.Users.Add(new User
        {
            Id = userId, FullName = "Admin", Email = $"admin{userId}@t.com",
            PhoneNumber = $"0179{userId:00000}", Role = UserRole.Admin, IsActive = true
        });
        await ctx.SaveChangesAsync();
        return new Admin(userId);
    }

    /// <summary>
    /// Seeds an organizer, tourist, an already-Completed OrganizedTrip with a paid
    /// (Held) registration escrow, and returns the handles tests need.
    /// </summary>
    private record TripSeed(
        int TouristUserId, int OrganizerUserId, int TripId, int RegistrationId,
        int PaymentId, decimal Amount);

    private static async Task<TripSeed> SeedPaidTripAsync(
        ApplicationDbContext ctx, string tag, int tripId = 100, int registrationId = 200,
        int touristUserId = 2)
    {
        const decimal price = 300m;

        if (await ctx.Users.FindAsync(1) is null)
        {
            ctx.Users.Add(new User
            {
                Id = 1, FullName = "Org", Email = "org@t.com", PhoneNumber = "01700000001",
                Role = UserRole.Organizer, IsActive = true
            });
        }
        if (await ctx.OrganizerProfiles.FindAsync(10) is null)
        {
            ctx.OrganizerProfiles.Add(new OrganizerProfile
            {
                Id = 10, UserId = 1, GroupOrganizationName = "TestOrg"
            });
        }
        if (await ctx.Users.FindAsync(touristUserId) is null)
        {
            ctx.Users.Add(new User
            {
                Id = touristUserId, FullName = $"T{touristUserId}", Email = $"t{touristUserId}@t.com",
                PhoneNumber = $"0170000{touristUserId:00}", Role = UserRole.Tourist, IsActive = true
            });
        }
        var touristProfile = new TouristProfile { Id = touristUserId * 10, UserId = touristUserId };
        ctx.TouristProfiles.Add(touristProfile);

        var trip = new OrganizedTrip
        {
            Id = tripId, OrganizerId = 10,
            Title = $"Trip {tag}", Destination = "Sylhet", Description = "Test",
            StartDate = PastDate(20), EndDate = PastDate(15),
            MaxParticipants = 10, PricePerPerson = price,
            Inclusions = "Food", RegistrationDeadline = FutureDate(5),
            Status = OrganizedTripStatus.RegistrationOpen, CreatedAt = DateTime.UtcNow
        };
        var registration = new Registration
        {
            Id = registrationId, OrganizedTripId = trip.Id, TouristId = touristProfile.Id,
            NumberOfTravelers = 1, Status = RegistrationStatus.PendingPayment,
            TotalPrice = price, RegisteredAt = DateTime.UtcNow
        };

        ctx.OrganizedTrips.Add(trip);
        ctx.Registrations.Add(registration);
        await ctx.SaveChangesAsync();

        var svc = BuildPaymentService(ctx);
        var deposit = await svc.PayForRegistrationAsync(touristUserId, registration.Id);
        Assert.True(deposit.Success, $"Deposit failed: {deposit.Message}");

        // Trip finishes; escrow is now releasable except where a dispute says otherwise.
        trip.Status = OrganizedTripStatus.Completed;
        await ctx.SaveChangesAsync();

        return new TripSeed(touristUserId, 1, trip.Id, registration.Id,
            deposit.Data!.PaymentId, price);
    }

    /// <summary>Seeds a Completed TripRequest with an Accepted proposal and a Held escrow payment.</summary>
    private record ProposalSeed(int TouristUserId, int GuideUserId, int TripRequestId, int ProposalId, int PaymentId);

    private static async Task<ProposalSeed> SeedPaidProposalAsync(
        ApplicationDbContext ctx, int tripRequestId = 300, int proposalId = 400)
    {
        var tourist = new User { Id = 3, FullName = "T3", Email = "t3@t.com", PhoneNumber = "01700000003", Role = UserRole.Tourist, IsActive = true };
        var guide = new User { Id = 4, FullName = "G4", Email = "g4@t.com", PhoneNumber = "01700000004", Role = UserRole.Guide, IsActive = true };
        var tripReq = new TripRequest
        {
            Id = tripRequestId, TouristId = tourist.Id,
            Destination = "Cox's Bazar", TripType = "Beach",
            StartDate = PastDate(10), EndDate = PastDate(5),
            NumberOfTravelers = 2, Budget = 600m, BudgetMin = 400m, BudgetMax = 700m,
            TravelPreferences = "Relaxed", Status = TripRequestStatus.Completed,
            MatchedGuideId = guide.Id
        };
        var proposal = new Proposal
        {
            Id = proposalId, TripRequestId = tripReq.Id, TripRequest = tripReq,
            GuideId = guide.Id, Guide = guide, ProposedPrice = 500m,
            Availability = "Anytime", Inclusions = "All", Exclusions = "None",
            Status = ProposalStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Users.AddRange(tourist, guide);
        ctx.TripRequests.Add(tripReq);
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();

        var svc = BuildPaymentService(ctx);
        var deposit = await svc.PayForProposalAsync(tourist.Id, proposal.Id);
        Assert.True(deposit.Success, $"Deposit failed: {deposit.Message}");

        return new ProposalSeed(tourist.Id, guide.Id, tripReq.Id, proposal.Id, deposit.Data!.PaymentId);
    }

    private static Dispute NewDispute(int id, int reporterUserId,
        DisputeRelatedTripType tripType, int tripId,
        DisputeStatus status = DisputeStatus.Open) => new()
    {
        Id = id,
        ReportedByUserId = reporterUserId,
        TripType = tripType,
        TripId = tripId,
        Reason = "Service not delivered",
        Description = "The hotel was not as described.",
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    // ─── Task 2: admin-only resolution ────────────────────────────────────────

    [Fact]
    public async Task Resolve_OpenDispute_ByAdmin_PersistsResolvedState()
    {
        await using var ctx = BuildContext(nameof(Resolve_OpenDispute_ByAdmin_PersistsResolvedState));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "A");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        var svc = BuildDisputeService(ctx);
        var result = await svc.ResolveDisputeAsync(admin.UserId, dispute.Id, "Organizer refunded the missed night.");

        Assert.True(result.Success);
        Assert.Equal(nameof(DisputeStatus.Resolved), result.Data!.Status);
        Assert.Equal("Organizer refunded the missed night.", result.Data.ResolutionNote);
        Assert.Equal(admin.UserId, result.Data.ResolvedByUserId);
        Assert.NotNull(result.Data.ResolvedAt);

        var saved = await ctx.Disputes.FirstAsync(d => d.Id == dispute.Id);
        Assert.Equal(DisputeStatus.Resolved, saved.Status);
        Assert.Equal(admin.UserId, saved.ResolvedByUserId);
        Assert.NotNull(saved.ResolvedAt);
    }

    [Theory]
    [InlineData(UserRole.Tourist)]
    [InlineData(UserRole.Organizer)]
    [InlineData(UserRole.Guide)]
    public async Task Resolve_ByNonAdminRole_ReturnsAdminOnly_AndStaysOpen(UserRole role)
    {
        await using var ctx = BuildContext($"{nameof(Resolve_ByNonAdminRole_ReturnsAdminOnly_AndStaysOpen)}_{role}");
        var seed = await SeedPaidTripAsync(ctx, "B");

        // A real user of the given role attempts the resolution.
        var caller = new User
        {
            Id = 700, FullName = $"Caller {role}", Email = "caller@t.com",
            PhoneNumber = "0170700700", Role = role, IsActive = true
        };
        ctx.Users.Add(caller);
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        var svc = BuildDisputeService(ctx);
        var result = await svc.ResolveDisputeAsync(caller.Id, dispute.Id, "Self-resolution attempt");

        Assert.False(result.Success);
        Assert.Equal(DisputeError.AdminOnly, result.Error);

        var saved = await ctx.Disputes.FirstAsync(d => d.Id == dispute.Id);
        Assert.Equal(DisputeStatus.Open, saved.Status);
        Assert.Null(saved.ResolutionNote);
        Assert.Null(saved.ResolvedByUserId);
        Assert.Null(saved.ResolvedAt);
    }

    [Fact]
    public async Task Resolve_CallerWithoutUser_ReturnsAdminOnly()
    {
        await using var ctx = BuildContext(nameof(Resolve_CallerWithoutUser_ReturnsAdminOnly));
        var seed = await SeedPaidTripAsync(ctx, "C");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        var svc = BuildDisputeService(ctx);
        var result = await svc.ResolveDisputeAsync(424242, dispute.Id, "Nobody");

        Assert.False(result.Success);
        Assert.Equal(DisputeError.AdminOnly, result.Error);
    }

    [Fact]
    public async Task Resolve_Twice_ReturnsDisputeAlreadyResolved_AndKeepsFirstNote()
    {
        await using var ctx = BuildContext(nameof(Resolve_Twice_ReturnsDisputeAlreadyResolved_AndKeepsFirstNote));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "D");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        var svc = BuildDisputeService(ctx);
        var first = await svc.ResolveDisputeAsync(admin.UserId, dispute.Id, "First account.");
        Assert.True(first.Success);
        var firstResolvedAt = (await ctx.Disputes.FirstAsync(d => d.Id == dispute.Id)).ResolvedAt;

        var second = await svc.ResolveDisputeAsync(admin.UserId, dispute.Id, "Rewritten account.");

        Assert.False(second.Success);
        Assert.Equal(DisputeError.DisputeAlreadyResolved, second.Error);

        var saved = await ctx.Disputes.FirstAsync(d => d.Id == dispute.Id);
        Assert.Equal(DisputeStatus.Resolved, saved.Status);
        Assert.Equal("First account.", saved.ResolutionNote);
        Assert.Equal(firstResolvedAt, saved.ResolvedAt);
    }

    [Fact]
    public async Task Resolve_ClosedDispute_ReturnsDisputeAlreadyResolved()
    {
        await using var ctx = BuildContext(nameof(Resolve_ClosedDispute_ReturnsDisputeAlreadyResolved));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "E");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip,
            seed.TripId, DisputeStatus.Closed);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx)
            .ResolveDisputeAsync(admin.UserId, dispute.Id, "Too late");

        Assert.False(result.Success);
        Assert.Equal(DisputeError.DisputeAlreadyResolved, result.Error);
    }

    [Fact]
    public async Task Resolve_UnknownDispute_ReturnsDisputeNotFound()
    {
        await using var ctx = BuildContext(nameof(Resolve_UnknownDispute_ReturnsDisputeNotFound));
        var admin = await SeedAdminAsync(ctx);

        var result = await BuildDisputeService(ctx)
            .ResolveDisputeAsync(admin.UserId, 424242, "Note");

        Assert.False(result.Success);
        Assert.Equal(DisputeError.DisputeNotFound, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Resolve_MissingResolutionNote_ReturnsInvalidInput(string? note)
    {
        await using var ctx = BuildContext($"{nameof(Resolve_MissingResolutionNote_ReturnsInvalidInput)}_{note?.Length ?? -1}");
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "F");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx).ResolveDisputeAsync(admin.UserId, dispute.Id, note);

        Assert.False(result.Success);
        Assert.Equal(DisputeError.InvalidInput, result.Error);
        Assert.Equal(DisputeStatus.Open, (await ctx.Disputes.FirstAsync(d => d.Id == dispute.Id)).Status);
    }

    [Fact]
    public async Task Resolve_OversizedResolutionNote_ReturnsInvalidInput()
    {
        await using var ctx = BuildContext(nameof(Resolve_OversizedResolutionNote_ReturnsInvalidInput));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "G");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        var result = await BuildDisputeService(ctx)
            .ResolveDisputeAsync(admin.UserId, dispute.Id, new string('x', 2001));

        Assert.False(result.Success);
        Assert.Equal(DisputeError.InvalidInput, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Resolve_NonPositiveDisputeId_ReturnsInvalidInput(int disputeId)
    {
        await using var ctx = BuildContext($"{nameof(Resolve_NonPositiveDisputeId_ReturnsInvalidInput)}_{disputeId}");
        var admin = await SeedAdminAsync(ctx);

        var result = await BuildDisputeService(ctx)
            .ResolveDisputeAsync(admin.UserId, disputeId, "Note");

        Assert.False(result.Success);
        Assert.Equal(DisputeError.InvalidInput, result.Error);
    }

    [Fact]
    public void ResolveEndpoint_IsAdminOnlyByAttributes()
    {
        var method = typeof(DisputesController).GetMethod(nameof(DisputesController.ResolveDispute));
        Assert.NotNull(method);

        // Route: POST /api/disputes/{disputeId}/resolve
        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("{disputeId:int}/resolve", post!.Template);

        // Role gate in front of the handler.
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);

        // The admin identity comes from the JWT claims inside the handler,
        // never from the request body.
        var disputeParam = method.GetParameters().Single(p => p.Name == "disputeId");
        Assert.NotNull(disputeParam.GetCustomAttribute<FromRouteAttribute>());

        var bodyParam = method.GetParameters().Single(p => p.Name == "request");
        Assert.NotNull(bodyParam.GetCustomAttribute<FromBodyAttribute>());
        Assert.Equal(typeof(ResolveDisputeDto), bodyParam.ParameterType);
    }

    // ─── Task 3: dispute rules during release ─────────────────────────────────

    [Fact]
    public async Task Release_OpenDispute_Blocked_ThenAdminResolves_ThenReleaseSucceeds()
    {
        await using var ctx = BuildContext(nameof(Release_OpenDispute_Blocked_ThenAdminResolves_ThenReleaseSucceeds));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "H");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        var payments = BuildPaymentService(ctx);
        var disputes = BuildDisputeService(ctx);

        var blocked = await payments.ReleaseEscrowAsync(seed.TouristUserId, seed.PaymentId);
        Assert.False(blocked.Success);
        Assert.Equal(PaymentError.DisputeBlocksRelease, blocked.Error);
        Assert.Equal(PaymentStatus.Held, (await ctx.Payments.FirstAsync(p => p.Id == seed.PaymentId)).Status);

        var resolved = await disputes.ResolveDisputeAsync(admin.UserId, dispute.Id, "Investigated; guide remedied.");
        Assert.True(resolved.Success);

        var released = await payments.ReleaseEscrowAsync(seed.TouristUserId, seed.PaymentId);
        Assert.True(released.Success, released.Message);
        Assert.Equal(PaymentStatus.Released, released.Data!.Status);
        Assert.NotNull(released.Data.EscrowReleasedAt);
    }

    [Fact]
    public async Task Release_ResolvedDispute_DoesNotBlock()
    {
        await using var ctx = BuildContext(nameof(Release_ResolvedDispute_DoesNotBlock));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "I");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        Assert.True((await BuildDisputeService(ctx)
            .ResolveDisputeAsync(admin.UserId, dispute.Id, "Settled out of band.")).Success);

        var release = await BuildPaymentService(ctx).ReleaseEscrowAsync(seed.TouristUserId, seed.PaymentId);

        Assert.True(release.Success, release.Message);
        Assert.Equal(PaymentStatus.Released, release.Data!.Status);
    }

    [Fact]
    public async Task Release_MilestoneOnDisputedTrip_IsBlocked()
    {
        await using var ctx = BuildContext(nameof(Release_MilestoneOnDisputedTrip_IsBlocked));
        var seed = await SeedPaidTripAsync(ctx, "J");
        var svc = BuildPaymentService(ctx);

        // Add a milestone to the held payment, then dispute the trip.
        var m1 = new PaymentMilestone { Id = 1, PaymentId = seed.PaymentId, Index = 1, Label = "Advance", Amount = 100m, Status = PaymentMilestoneStatus.Held, CreatedAt = DateTime.UtcNow, HeldAt = DateTime.UtcNow };
        ctx.PaymentMilestones.Add(m1);
        ctx.Disputes.Add(NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId));
        await ctx.SaveChangesAsync();

        var release = await svc.ReleaseMilestoneAsync(seed.TouristUserId, seed.PaymentId, m1.Id);

        Assert.False(release.Success);
        Assert.Equal(PaymentError.DisputeBlocksRelease, release.Error);
    }

    // ─── Task 3: dispute rules during refund ──────────────────────────────────

    [Fact]
    public async Task Refund_OpenDispute_RequiresResolution_AndChangesNothing()
    {
        await using var ctx = BuildContext(nameof(Refund_OpenDispute_RequiresResolution_AndChangesNothing));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "K");
        ctx.Disputes.Add(NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId));
        await ctx.SaveChangesAsync();

        var refund = await BuildPaymentService(ctx)
            .RefundAsync(admin.UserId, seed.PaymentId, 100m, "Refund while disputed");

        Assert.False(refund.Success);
        Assert.Equal(PaymentError.DisputeResolutionRequired, refund.Error);

        var saved = await ctx.Payments.FirstAsync(p => p.Id == seed.PaymentId);
        Assert.Equal(PaymentStatus.Held, saved.Status);
        Assert.Null(saved.RefundedAmount);
        Assert.Null(saved.RefundReason);
    }

    [Fact]
    public async Task Refund_AfterAdminResolves_IsAllowed()
    {
        await using var ctx = BuildContext(nameof(Refund_AfterAdminResolves_IsAllowed));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "L");
        var dispute = NewDispute(1, seed.TouristUserId, DisputeRelatedTripType.OrganizedTrip, seed.TripId);
        ctx.Disputes.Add(dispute);
        await ctx.SaveChangesAsync();

        Assert.True((await BuildDisputeService(ctx)
            .ResolveDisputeAsync(admin.UserId, dispute.Id, "Refund agreed with both parties.")).Success);

        var refund = await BuildPaymentService(ctx)
            .RefundAsync(admin.UserId, seed.PaymentId, 100m, "Agreed partial refund");

        Assert.True(refund.Success, refund.Message);
        Assert.Equal(PaymentStatus.PartiallyRefunded, refund.Data!.Status);
        Assert.Equal(100m, refund.Data.RefundedAmount);
    }

    [Fact]
    public async Task Refund_UndisputedPayment_IsAllowed()
    {
        await using var ctx = BuildContext(nameof(Refund_UndisputedPayment_IsAllowed));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "M");

        var refund = await BuildPaymentService(ctx)
            .RefundAsync(admin.UserId, seed.PaymentId, null, "Trip cancelled by organizer");

        Assert.True(refund.Success, refund.Message);
        Assert.Equal(PaymentStatus.Refunded, refund.Data!.Status);
        Assert.Equal(seed.Amount, refund.Data.RefundedAmount);
    }

    // ─── Task 3: dispute scoping must not leak across trips ──────────────────

    [Fact]
    public async Task OpenDispute_OnAnotherOrganizedTrip_DoesNotBlockThisPayment()
    {
        await using var ctx = BuildContext(nameof(OpenDispute_OnAnotherOrganizedTrip_DoesNotBlockThisPayment));
        var seed = await SeedPaidTripAsync(ctx, "N", tripId: 100, registrationId: 200, touristUserId: 2);

        // A second, unrelated organized trip carrying the open dispute.
        var otherTrip = new OrganizedTrip
        {
            Id = 101, OrganizerId = 10, Title = "Other Trip", Destination = "Rangamati",
            Description = "Test", StartDate = PastDate(20), EndDate = PastDate(15),
            MaxParticipants = 5, PricePerPerson = 200m, Inclusions = "Food",
            RegistrationDeadline = FutureDate(5),
            Status = OrganizedTripStatus.Completed, CreatedAt = DateTime.UtcNow
        };
        ctx.OrganizedTrips.Add(otherTrip);
        ctx.Disputes.Add(NewDispute(1, 2, DisputeRelatedTripType.OrganizedTrip, otherTrip.Id));
        await ctx.SaveChangesAsync();

        var release = await BuildPaymentService(ctx).ReleaseEscrowAsync(seed.TouristUserId, seed.PaymentId);

        Assert.True(release.Success, release.Message);
        Assert.Equal(PaymentStatus.Released, release.Data!.Status);
    }

    [Fact]
    public async Task OpenDispute_OnTripRequest_DoesNotBlockOrganizedTripPayment()
    {
        await using var ctx = BuildContext(nameof(OpenDispute_OnTripRequest_DoesNotBlockOrganizedTripPayment));
        var seed = await SeedPaidTripAsync(ctx, "O");
        var proposalSeed = await SeedPaidProposalAsync(ctx);

        // Dispute raised against the guide trip request only.
        ctx.Disputes.Add(NewDispute(1, proposalSeed.TouristUserId,
            DisputeRelatedTripType.TripRequest, proposalSeed.TripRequestId));
        await ctx.SaveChangesAsync();

        var payments = BuildPaymentService(ctx);

        var blocked = await payments.ReleaseEscrowAsync(proposalSeed.TouristUserId, proposalSeed.PaymentId);
        Assert.False(blocked.Success);
        Assert.Equal(PaymentError.DisputeBlocksRelease, blocked.Error);

        var unaffected = await payments.ReleaseEscrowAsync(seed.TouristUserId, seed.PaymentId);
        Assert.True(unaffected.Success, unaffected.Message);
        Assert.Equal(PaymentStatus.Released, unaffected.Data!.Status);
    }

    [Fact]
    public async Task OpenDispute_OnOrganizedTrip_DoesNotBlockTripRequestPayment()
    {
        await using var ctx = BuildContext(nameof(OpenDispute_OnOrganizedTrip_DoesNotBlockTripRequestPayment));
        var seed = await SeedPaidTripAsync(ctx, "P");
        var proposalSeed = await SeedPaidProposalAsync(ctx);

        ctx.Disputes.Add(NewDispute(1, seed.TouristUserId,
            DisputeRelatedTripType.OrganizedTrip, seed.TripId));
        await ctx.SaveChangesAsync();

        var payments = BuildPaymentService(ctx);

        var blocked = await payments.ReleaseEscrowAsync(seed.TouristUserId, seed.PaymentId);
        Assert.False(blocked.Success);
        Assert.Equal(PaymentError.DisputeBlocksRelease, blocked.Error);

        var unaffected = await payments.ReleaseEscrowAsync(proposalSeed.TouristUserId, proposalSeed.PaymentId);
        Assert.True(unaffected.Success, unaffected.Message);
        Assert.Equal(PaymentStatus.Released, unaffected.Data!.Status);
    }

    [Fact]
    public async Task OpenDispute_BlocksRefundOnDisputedTrip_ButNotOnSiblingTrip()
    {
        await using var ctx = BuildContext(nameof(OpenDispute_BlocksRefundOnDisputedTrip_ButNotOnSiblingTrip));
        var admin = await SeedAdminAsync(ctx);
        var disputed = await SeedPaidTripAsync(ctx, "Q", tripId: 100, registrationId: 200, touristUserId: 2);
        var sibling = await SeedPaidTripAsync(ctx, "R", tripId: 101, registrationId: 201, touristUserId: 3);

        ctx.Disputes.Add(NewDispute(1, disputed.TouristUserId,
            DisputeRelatedTripType.OrganizedTrip, disputed.TripId));
        await ctx.SaveChangesAsync();

        var payments = BuildPaymentService(ctx);

        var blockedRefund = await payments.RefundAsync(admin.UserId, disputed.PaymentId, 50m, "While disputed");
        Assert.False(blockedRefund.Success);
        Assert.Equal(PaymentError.DisputeResolutionRequired, blockedRefund.Error);

        var allowedRefund = await payments.RefundAsync(admin.UserId, sibling.PaymentId, 50m, "Undisputed");
        Assert.True(allowedRefund.Success, allowedRefund.Message);
        Assert.Equal(PaymentStatus.PartiallyRefunded, allowedRefund.Data!.Status);
    }

    // ─── Task 7: authorization conventions preserved ──────────────────────────

    [Fact]
    public async Task RaiseDispute_ThenAdminResolve_KeepsReporterAndAuditFields()
    {
        await using var ctx = BuildContext(nameof(RaiseDispute_ThenAdminResolve_KeepsReporterAndAuditFields));
        var admin = await SeedAdminAsync(ctx);
        var seed = await SeedPaidTripAsync(ctx, "S");

        var disputes = BuildDisputeService(ctx);
        var raised = await disputes.RaiseDisputeAsync(seed.TouristUserId, UserRole.Tourist,
            new RaiseDisputeDto
            {
                TripType = DisputeRelatedTripType.OrganizedTrip,
                TripId = seed.TripId,
                Reason = "Hotel not as described",
                Description = "Room category was downgraded on arrival."
            });
        Assert.True(raised.Success, raised.Message);
        Assert.Equal(nameof(DisputeStatus.Open), raised.Data!.Status);
        Assert.Null(raised.Data.ResolvedByUserId);

        var resolved = await disputes.ResolveDisputeAsync(admin.UserId, raised.Data.Id,
            "Organizer issued a 20% credit.");
        Assert.True(resolved.Success);
        Assert.Equal(raised.Data.ReportedByUserId, seed.TouristUserId);
        Assert.Equal(admin.UserId, resolved.Data!.ResolvedByUserId);
        Assert.Equal(DisputeStatus.Resolved, (await ctx.Disputes.FirstAsync(d => d.Id == raised.Data.Id)).Status);

        // Resolution unfreezes the escrow for the trip it belongs to.
        var release = await BuildPaymentService(ctx).ReleaseEscrowAsync(seed.TouristUserId, seed.PaymentId);
        Assert.True(release.Success, release.Message);
    }
}
