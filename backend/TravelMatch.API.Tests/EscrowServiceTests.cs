using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for the escrow lifecycle in PaymentService:
///   deposit → Held, release, full refund, partial refund,
///   duplicate operations, dispute-block, authorization, cross-user,
///   get-my-payments (tourist), get-trip-payments (organizer).
/// Uses EF Core InMemory — no real database required.
/// </summary>
public class EscrowServiceTests
{
    // ─── Infrastructure ──────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static PaymentService BuildService(ApplicationDbContext ctx) =>
        new PaymentService(ctx, new SandboxPaymentGateway(), NullLogger<PaymentService>.Instance);

    private static DateOnly FutureDate(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days));

    // ─── Seed helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds tourist + profile, organizer + profile, an OrganizedTrip in the given
    /// status, and a Registration for the tourist in PendingPayment.
    /// Returns ids needed by tests.
    /// </summary>
    private static async Task<RegSeed> SeedRegistrationScenarioAsync(
        ApplicationDbContext ctx,
        OrganizedTripStatus tripStatus = OrganizedTripStatus.RegistrationOpen,
        int maxParticipants = 10,
        int deadlineDaysFromNow = 5,
        decimal pricePerPerson = 300m)
    {
        var orgUser = new User { Id = 1, FullName = "Org", Email = "org@t.com", PhoneNumber = "01700000001", Role = UserRole.Organizer, IsActive = true };
        var orgProfile = new OrganizerProfile { Id = 10, UserId = orgUser.Id, GroupOrganizationName = "TestOrg" };
        var touristUser = new User { Id = 2, FullName = "Tourist", Email = "t@t.com", PhoneNumber = "01700000002", Role = UserRole.Tourist, IsActive = true };
        var touristProfile = new TouristProfile { Id = 20, UserId = touristUser.Id };
        var trip = new OrganizedTrip
        {
            Id = 100, OrganizerId = orgProfile.Id,
            Title = "Escrow Trip", Destination = "Sylhet", Description = "Test",
            StartDate = FutureDate(15), EndDate = FutureDate(20),
            MaxParticipants = maxParticipants, PricePerPerson = pricePerPerson,
            Inclusions = "Food",
            RegistrationDeadline = FutureDate(deadlineDaysFromNow),
            Status = tripStatus, CreatedAt = DateTime.UtcNow
        };
        var registration = new Registration
        {
            Id = 200, OrganizedTripId = trip.Id, TouristId = touristProfile.Id,
            NumberOfTravelers = 1, Status = RegistrationStatus.PendingPayment,
            TotalPrice = pricePerPerson, RegisteredAt = DateTime.UtcNow
        };
        ctx.Users.AddRange(orgUser, touristUser);
        ctx.OrganizerProfiles.Add(orgProfile);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.OrganizedTrips.Add(trip);
        ctx.Registrations.Add(registration);
        await ctx.SaveChangesAsync();
        return new RegSeed(touristUser.Id, touristProfile.Id, orgUser.Id, orgProfile.Id, trip.Id, registration.Id);
    }

    private record RegSeed(
        int TouristUserId, int TouristProfileId,
        int OrganizerUserId, int OrganizerProfileId,
        int TripId, int RegistrationId);

    /// <summary>
    /// Seeds a tourist, guide, TripRequest (Completed), and Proposal (Accepted).
    /// </summary>
    private static async Task<ProposalSeed> SeedProposalScenarioAsync(
        ApplicationDbContext ctx,
        TripRequestStatus tripStatus = TripRequestStatus.Completed,
        decimal proposedPrice = 500m)
    {
        var tourist = new User { Id = 3, FullName = "T2", Email = "t2@t.com", PhoneNumber = "01700000003", Role = UserRole.Tourist, IsActive = true };
        var guide   = new User { Id = 4, FullName = "G1", Email = "g1@t.com", PhoneNumber = "01700000004", Role = UserRole.Guide,   IsActive = true };
        var tripReq = new TripRequest
        {
            Id = 300, TouristId = tourist.Id,
            Destination = "Cox's Bazar", TripType = "Beach",
            StartDate = FutureDate(-10), EndDate = FutureDate(-5),
            NumberOfTravelers = 2, Budget = 600m, BudgetMin = 400m, BudgetMax = 700m,
            TravelPreferences = "Relaxed", Status = tripStatus,
            MatchedGuideId = guide.Id
        };
        var proposal = new Proposal
        {
            Id = 400, TripRequestId = tripReq.Id, TripRequest = tripReq,
            GuideId = guide.Id, Guide = guide,
            ProposedPrice = proposedPrice,
            Availability = "Anytime", Inclusions = "All", Exclusions = "None",
            Status = ProposalStatus.Accepted
        };
        ctx.Users.AddRange(tourist, guide);
        ctx.TripRequests.Add(tripReq);
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();
        return new ProposalSeed(tourist.Id, guide.Id, tripReq.Id, proposal.Id);
    }

    private record ProposalSeed(int TouristUserId, int GuideUserId, int TripRequestId, int ProposalId);

    /// <summary>
    /// Deposits successfully and returns the persisted Payment id.
    /// </summary>
    private static async Task<int> DepositForRegistrationAsync(
        PaymentService svc, ApplicationDbContext ctx,
        int touristUserId, int registrationId)
    {
        var result = await svc.PayForRegistrationAsync(touristUserId, registrationId);
        Assert.True(result.Success, $"Deposit failed: {result.Message}");
        return result.Data!.PaymentId;
    }

    private static async Task<int> DepositForProposalAsync(
        PaymentService svc, int touristUserId, int proposalId)
    {
        var result = await svc.PayForProposalAsync(touristUserId, proposalId);
        Assert.True(result.Success, $"Deposit failed: {result.Message}");
        return result.Data!.PaymentId;
    }

    // ─── Deposit → Held ───────────────────────────────────────────────────────

    [Fact]
    public async Task Deposit_Registration_SetsStatusHeld_AndConfirmsRegistration()
    {
        await using var ctx = BuildContext(nameof(Deposit_Registration_SetsStatusHeld_AndConfirmsRegistration));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var result = await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Held, result.Data!.Status);
        Assert.NotNull(result.Data.CompletedAt);
        Assert.Null(result.Data.EscrowReleasedAt);

        var reg = await ctx.Registrations.FindAsync(seed.RegistrationId);
        Assert.Equal(RegistrationStatus.Confirmed, reg!.Status);
    }

    [Fact]
    public async Task Deposit_Proposal_SetsStatusHeld()
    {
        await using var ctx = BuildContext(nameof(Deposit_Proposal_SetsStatusHeld));
        var seed = await SeedProposalScenarioAsync(ctx, tripStatus: TripRequestStatus.Matched);
        // Proposal must be Accepted, but trip doesn't need to be Completed to deposit
        var tripReq = await ctx.TripRequests.FindAsync(seed.TripRequestId);
        tripReq!.Status = TripRequestStatus.PaymentPending;
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.PayForProposalAsync(seed.TouristUserId, seed.ProposalId);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Held, result.Data!.Status);
        Assert.NotNull(result.Data.CompletedAt);
    }

    [Fact]
    public async Task Deposit_SimulatedGatewayFailure_LeavesStatusFailed()
    {
        await using var ctx = BuildContext(nameof(Deposit_SimulatedGatewayFailure_LeavesStatusFailed));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var result = await svc.PayForRegistrationAsync(
            seed.TouristUserId, seed.RegistrationId,
            new PayRegistrationRequestDto { PaymentMethod = "FAIL" });

        Assert.False(result.Success);
        Assert.Equal(PaymentError.PaymentFailed, result.Error);
        Assert.Equal(PaymentStatus.Failed, result.Data!.Status);

        // Registration must remain PendingPayment
        var reg = await ctx.Registrations.FindAsync(seed.RegistrationId);
        Assert.Equal(RegistrationStatus.PendingPayment, reg!.Status);
    }

    [Fact]
    public async Task Deposit_Duplicate_ReturnsAlreadyPaid()
    {
        await using var ctx = BuildContext(nameof(Deposit_Duplicate_ReturnsAlreadyPaid));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);
        var second = await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);

        Assert.False(second.Success);
        Assert.Equal(PaymentError.AlreadyPaid, second.Error);
    }

    // ─── Release escrow ───────────────────────────────────────────────────────

    [Fact]
    public async Task Release_AfterTripCompleted_SetsStatusReleased()
    {
        await using var ctx = BuildContext(nameof(Release_AfterTripCompleted_SetsStatusReleased));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        // Mark trip as Completed
        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.Completed;
        await ctx.SaveChangesAsync();

        var result = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Released, result.Data!.Status);
        Assert.NotNull(result.Data.EscrowReleasedAt);
    }

    [Fact]
    public async Task Release_ProposalPayment_AfterTripRequestCompleted_Succeeds()
    {
        await using var ctx = BuildContext(nameof(Release_ProposalPayment_AfterTripRequestCompleted_Succeeds));
        var seed = await SeedProposalScenarioAsync(ctx, tripStatus: TripRequestStatus.PaymentPending);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForProposalAsync(svc, seed.TouristUserId, seed.ProposalId);

        // Advance TripRequest to Completed
        var tripReq = await ctx.TripRequests.FindAsync(seed.TripRequestId);
        tripReq!.Status = TripRequestStatus.Completed;
        await ctx.SaveChangesAsync();

        var result = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Released, result.Data!.Status);
    }

    [Fact]
    public async Task Release_TripNotCompleted_ReturnsTripNotCompleted()
    {
        await using var ctx = BuildContext(nameof(Release_TripNotCompleted_ReturnsTripNotCompleted));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);
        // Trip is still RegistrationOpen — not completed

        var result = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.TripNotCompleted, result.Error);
    }

    [Fact]
    public async Task Release_WrongTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Release_WrongTourist_ReturnsUnauthorized));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.Completed;
        await ctx.SaveChangesAsync();

        var result = await svc.ReleaseEscrowAsync(9999, paymentId);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task Release_PaymentNotFound_ReturnsPaymentNotFound()
    {
        await using var ctx = BuildContext(nameof(Release_PaymentNotFound_ReturnsPaymentNotFound));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var result = await svc.ReleaseEscrowAsync(seed.TouristUserId, 9999);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.PaymentNotFound, result.Error);
    }

    [Fact]
    public async Task Release_DuplicateRelease_ReturnsEscrowAlreadySettled()
    {
        await using var ctx = BuildContext(nameof(Release_DuplicateRelease_ReturnsEscrowAlreadySettled));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.Completed;
        await ctx.SaveChangesAsync();

        await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);
        var second = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.False(second.Success);
        Assert.Equal(PaymentError.EscrowAlreadySettled, second.Error);
    }

    [Fact]
    public async Task Release_OpenDisputeOnTrip_ReturnsDisputeBlocksRelease()
    {
        await using var ctx = BuildContext(nameof(Release_OpenDisputeOnTrip_ReturnsDisputeBlocksRelease));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        // Mark trip Completed
        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.Completed;

        // Seed an open dispute for this trip
        var disputingUser = new User { Id = 99, FullName = "Disputant", Email = "d@t.com", PhoneNumber = "018", Role = UserRole.Tourist, IsActive = true };
        ctx.Users.Add(disputingUser);
        ctx.Disputes.Add(new Dispute
        {
            Id = 1, ReportedByUserId = disputingUser.Id,
            TripType = DisputeRelatedTripType.OrganizedTrip,
            TripId = seed.TripId,
            Reason = "Bad service", Description = "Details",
            Status = DisputeStatus.Open, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.DisputeBlocksRelease, result.Error);
    }

    [Fact]
    public async Task Release_ResolvedDisputeDoesNotBlock()
    {
        await using var ctx = BuildContext(nameof(Release_ResolvedDisputeDoesNotBlock));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.Completed;

        var disputingUser = new User { Id = 99, FullName = "D", Email = "d@t.com", PhoneNumber = "018", Role = UserRole.Tourist, IsActive = true };
        ctx.Users.Add(disputingUser);
        ctx.Disputes.Add(new Dispute
        {
            Id = 1, ReportedByUserId = disputingUser.Id,
            TripType = DisputeRelatedTripType.OrganizedTrip,
            TripId = seed.TripId,
            Reason = "Bad service", Description = "Details",
            Status = DisputeStatus.Resolved, // resolved — must NOT block
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Released, result.Data!.Status);
    }

    // ─── Full refund ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Refund_Full_SetsStatusRefunded()
    {
        await using var ctx = BuildContext(nameof(Refund_Full_SetsStatusRefunded));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 400m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        // null amount → full refund
        var result = await svc.RefundAsync(adminUserId: 1, paymentId, amount: null, reason: "Organizer cancelled");

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Refunded, result.Data!.Status);
        Assert.Equal(400m, result.Data.RefundedAmount);
        Assert.NotNull(result.Data.RefundedAt);
    }

    [Fact]
    public async Task Refund_Partial_SetsStatusPartiallyRefunded()
    {
        await using var ctx = BuildContext(nameof(Refund_Partial_SetsStatusPartiallyRefunded));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 400m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var result = await svc.RefundAsync(adminUserId: 1, paymentId, amount: 150m, reason: "Partial service");

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.PartiallyRefunded, result.Data!.Status);
        Assert.Equal(150m, result.Data.RefundedAmount);
    }

    [Fact]
    public async Task Refund_TwoPartialRefunds_AccumulateAndEventuallyFullyRefunded()
    {
        await using var ctx = BuildContext(nameof(Refund_TwoPartialRefunds_AccumulateAndEventuallyFullyRefunded));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 400m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        // First partial: 200
        var first = await svc.RefundAsync(1, paymentId, 200m, "First");
        Assert.True(first.Success);
        Assert.Equal(PaymentStatus.PartiallyRefunded, first.Data!.Status);
        Assert.Equal(200m, first.Data.RefundedAmount);

        // Second partial: 200 (exhausts the remaining 200 → fully refunded)
        var second = await svc.RefundAsync(1, paymentId, 200m, "Second");
        Assert.True(second.Success);
        Assert.Equal(PaymentStatus.Refunded, second.Data!.Status);
        Assert.Equal(400m, second.Data.RefundedAmount);
    }

    [Fact]
    public async Task Refund_ExceedsHeldAmount_ReturnsRefundExceedsHeldAmount()
    {
        await using var ctx = BuildContext(nameof(Refund_ExceedsHeldAmount_ReturnsRefundExceedsHeldAmount));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 300m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var result = await svc.RefundAsync(1, paymentId, amount: 500m, reason: null);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.RefundExceedsHeldAmount, result.Error);
    }

    [Fact]
    public async Task Refund_AfterFullRefund_ReturnsDuplicate()
    {
        await using var ctx = BuildContext(nameof(Refund_AfterFullRefund_ReturnsDuplicate));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 300m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);
        await svc.RefundAsync(1, paymentId, null, "Cancel");

        var second = await svc.RefundAsync(1, paymentId, null, "Again");

        Assert.False(second.Success);
        Assert.Equal(PaymentError.EscrowAlreadySettled, second.Error);
    }

    [Fact]
    public async Task Refund_OnReleasedPayment_ReturnsNotRefundable()
    {
        await using var ctx = BuildContext(nameof(Refund_OnReleasedPayment_ReturnsNotRefundable));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.Completed;
        await ctx.SaveChangesAsync();

        await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        var result = await svc.RefundAsync(1, paymentId, null, "Too late");

        Assert.False(result.Success);
        Assert.Equal(PaymentError.NotRefundable, result.Error);
    }

    [Fact]
    public async Task Refund_OnFailedPayment_ReturnsNotRefundable()
    {
        await using var ctx = BuildContext(nameof(Refund_OnFailedPayment_ReturnsNotRefundable));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var failResult = await svc.PayForRegistrationAsync(
            seed.TouristUserId, seed.RegistrationId,
            new PayRegistrationRequestDto { PaymentMethod = "FAIL" });
        Assert.False(failResult.Success);

        var paymentId = failResult.Data!.PaymentId;

        var result = await svc.RefundAsync(1, paymentId, null, "Refund failed payment");

        Assert.False(result.Success);
        Assert.Equal(PaymentError.NotRefundable, result.Error);
    }

    [Fact]
    public async Task Refund_PaymentNotFound_ReturnsPaymentNotFound()
    {
        await using var ctx = BuildContext(nameof(Refund_PaymentNotFound_ReturnsPaymentNotFound));
        var svc = BuildService(ctx);

        var result = await svc.RefundAsync(1, 9999, null, null);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.PaymentNotFound, result.Error);
    }

    // ─── Refund validation (Task 6) ───────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-300)]
    public async Task Refund_NonPositiveAmount_ReturnsInvalidRequest_AndChangesNothing(decimal amount)
    {
        await using var ctx = BuildContext($"{nameof(Refund_NonPositiveAmount_ReturnsInvalidRequest_AndChangesNothing)}_{amount}");
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 300m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var result = await svc.RefundAsync(1, paymentId, amount, "Bad amount");

        Assert.False(result.Success);
        Assert.Equal(PaymentError.InvalidRequest, result.Error);

        var saved = await ctx.Payments.FindAsync(paymentId);
        Assert.Equal(PaymentStatus.Held, saved!.Status);
        Assert.Null(saved.RefundedAmount);
        Assert.Null(saved.RefundedAt);
        Assert.Null(saved.RefundReason);
    }

    [Fact]
    public async Task Refund_RepeatedPartials_CannotExceedOriginalAmount()
    {
        await using var ctx = BuildContext(nameof(Refund_RepeatedPartials_CannotExceedOriginalAmount));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 400m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        Assert.True((await svc.RefundAsync(1, paymentId, 150m, "First")).Success);
        Assert.True((await svc.RefundAsync(1, paymentId, 150m, "Second")).Success);

        // Only 100 remains; a third 150 must be refused, not clamped.
        var third = await svc.RefundAsync(1, paymentId, 150m, "Third");
        Assert.False(third.Success);
        Assert.Equal(PaymentError.RefundExceedsHeldAmount, third.Error);

        var saved = await ctx.Payments.FindAsync(paymentId);
        Assert.Equal(300m, saved!.RefundedAmount);
        Assert.Equal(PaymentStatus.PartiallyRefunded, saved.Status);

        // The exact remaining balance is refundable and closes the escrow.
        var final = await svc.RefundAsync(1, paymentId, 100m, "Remainder");
        Assert.True(final.Success);
        Assert.Equal(400m, final.Data!.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, final.Data.Status);

        // Nothing further can be refunded once the original amount is fully returned.
        var overSettled = await svc.RefundAsync(1, paymentId, 1m, "Extra");
        Assert.False(overSettled.Success);
        Assert.Equal(PaymentError.EscrowAlreadySettled, overSettled.Error);
    }

    [Fact]
    public async Task Refund_OmittedAmountRefundsExactlyTheRemainingBalance()
    {
        await using var ctx = BuildContext(nameof(Refund_OmittedAmountRefundsExactlyTheRemainingBalance));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 400m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);
        await svc.RefundAsync(1, paymentId, 100m, "First");

        var second = await svc.RefundAsync(1, paymentId, null, "Rest");

        Assert.True(second.Success);
        Assert.Equal(400m, second.Data!.RefundedAmount);
        Assert.Equal(PaymentStatus.Refunded, second.Data.Status);
    }

    [Fact]
    public async Task Refund_StoresRefundReason_WithoutTouchingGatewayFailureReason()
    {
        await using var ctx = BuildContext(nameof(Refund_StoresRefundReason_WithoutTouchingGatewayFailureReason));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 300m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var result = await svc.RefundAsync(1, paymentId, 120m, "  Two nights not delivered  ");

        Assert.True(result.Success);
        Assert.Equal("Two nights not delivered", result.Data!.RefundReason);
        Assert.Null(result.Data.FailureReason);

        var saved = await ctx.Payments.FindAsync(paymentId);
        Assert.Equal("Two nights not delivered", saved!.RefundReason);
        Assert.Null(saved.FailureReason);
        Assert.NotNull(saved.RefundedAt);
        Assert.Equal(120m, saved.RefundedAmount);
    }

    [Fact]
    public async Task Refund_BlankReason_StoresNoRefundReason()
    {
        await using var ctx = BuildContext(nameof(Refund_BlankReason_StoresNoRefundReason));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 300m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var result = await svc.RefundAsync(1, paymentId, 50m, "   ");

        Assert.True(result.Success);
        Assert.Null(result.Data!.RefundReason);
        Assert.Null((await ctx.Payments.FindAsync(paymentId))!.RefundReason);
    }

    [Fact]
    public async Task Refund_AfterPreviousGatewayFailure_KeepsFailureReasonSeparate()
    {
        await using var ctx = BuildContext(nameof(Refund_AfterPreviousGatewayFailure_KeepsFailureReasonSeparate));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 300m);
        var svc  = BuildService(ctx);

        // A declined attempt records a FailureReason on its own Failed payment…
        var declined = await svc.PayForRegistrationAsync(
            seed.TouristUserId, seed.RegistrationId,
            new PayRegistrationRequestDto { PaymentMethod = "DECLINED" });
        Assert.False(declined.Success);
        var failedPaymentId = declined.Data!.PaymentId;
        Assert.NotNull((await ctx.Payments.FindAsync(failedPaymentId))!.FailureReason);

        // …while the successful re-payment carries the admin's refund reason only.
        var deposit = await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);
        Assert.True(deposit.Success);

        var refund = await svc.RefundAsync(1, deposit.Data!.PaymentId, null, "Trip called off");

        Assert.True(refund.Success);
        Assert.Equal("Trip called off", refund.Data!.RefundReason);
        Assert.Null(refund.Data.FailureReason);

        var savedDeposit = await ctx.Payments.FindAsync(deposit.Data.PaymentId);
        Assert.Null(savedDeposit!.FailureReason);
        Assert.Equal("Trip called off", savedDeposit.RefundReason);
    }

    // ─── GetMyPayments (tourist) ──────────────────────────────────────────────

    [Fact]
    public async Task GetMyPayments_ReturnsOnlyOwnerPayments()
    {
        await using var ctx = BuildContext(nameof(GetMyPayments_ReturnsOnlyOwnerPayments));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        // Tourist makes one deposit
        await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);

        // Seed a second tourist with a payment that must NOT appear
        var other = new User { Id = 50, FullName = "Other", Email = "o@t.com", PhoneNumber = "019", Role = UserRole.Tourist, IsActive = true };
        ctx.Users.Add(other);
        ctx.Payments.Add(new Payment
        {
            Id = 9999, TouristUserId = other.Id,
            TargetType = PaymentTargetType.Registration,
            Amount = 100m, Currency = "USD",
            Status = PaymentStatus.Held,
            TransactionReference = "X",
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await svc.GetMyPaymentsAsync(seed.TouristUserId);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Data, p => Assert.Equal(seed.TouristUserId, p.TouristUserId));
    }

    [Fact]
    public async Task GetMyPayments_NoPayments_ReturnsSuccessEmptyList()
    {
        await using var ctx = BuildContext(nameof(GetMyPayments_NoPayments_ReturnsSuccessEmptyList));
        var svc = BuildService(ctx);

        var result = await svc.GetMyPaymentsAsync(42);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
    }

    // ─── GetTripPayments (organizer) ──────────────────────────────────────────

    [Fact]
    public async Task GetTripPayments_Organizer_OwnsTrip_ReturnsPayments()
    {
        await using var ctx = BuildContext(nameof(GetTripPayments_Organizer_OwnsTrip_ReturnsPayments));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);

        var result = await svc.GetTripPaymentsAsync(seed.OrganizerUserId, seed.TripId);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetTripPayments_WrongOrganizer_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(GetTripPayments_WrongOrganizer_ReturnsUnauthorized));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        // Seed a second organizer who does not own the trip
        var org2User = new User { Id = 77, FullName = "Org2", Email = "o2@t.com", PhoneNumber = "017", Role = UserRole.Organizer, IsActive = true };
        var org2Prof = new OrganizerProfile { Id = 770, UserId = org2User.Id, GroupOrganizationName = "Other" };
        ctx.Users.Add(org2User);
        ctx.OrganizerProfiles.Add(org2Prof);
        await ctx.SaveChangesAsync();

        var result = await svc.GetTripPaymentsAsync(org2User.Id, seed.TripId);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task GetTripPayments_TripNotFound_ReturnsTripNotFound()
    {
        await using var ctx = BuildContext(nameof(GetTripPayments_TripNotFound_ReturnsTripNotFound));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var result = await svc.GetTripPaymentsAsync(seed.OrganizerUserId, 9999);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.OrganizedTripNotFound, result.Error);
    }

    [Fact]
    public async Task GetTripPayments_NoOrganizerProfile_ReturnsOrganizerProfileNotFound()
    {
        await using var ctx = BuildContext(nameof(GetTripPayments_NoOrganizerProfile_ReturnsOrganizerProfileNotFound));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var result = await svc.GetTripPaymentsAsync(9999, seed.TripId);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.OrganizerProfileNotFound, result.Error);
    }

    // ─── GetPaymentsByUser (admin) ────────────────────────────────────────────

    [Fact]
    public async Task GetPaymentsByUser_Admin_ReturnsAllPaymentsForUser()
    {
        await using var ctx = BuildContext(nameof(GetPaymentsByUser_Admin_ReturnsAllPaymentsForUser));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);

        var result = await svc.GetPaymentsByUserAsync(seed.TouristUserId);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(seed.TouristUserId, result.Data[0].TouristUserId);
    }

    [Fact]
    public async Task GetPaymentsByUser_NoPayments_ReturnsSuccessEmptyList()
    {
        await using var ctx = BuildContext(nameof(GetPaymentsByUser_NoPayments_ReturnsSuccessEmptyList));
        var svc = BuildService(ctx);

        var result = await svc.GetPaymentsByUserAsync(42);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
    }

    // ─── Payment response DTO escrow fields ───────────────────────────────────

    [Fact]
    public async Task Deposit_ResponseDto_EscrowFieldsAreNull_UntilReleaseOrRefund()
    {
        await using var ctx = BuildContext(nameof(Deposit_ResponseDto_EscrowFieldsAreNull_UntilReleaseOrRefund));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var depositResult = await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);

        Assert.Null(depositResult.Data!.EscrowReleasedAt);
        Assert.Null(depositResult.Data.RefundedAmount);
        Assert.Null(depositResult.Data.RefundedAt);
    }

    [Fact]
    public async Task Release_ResponseDto_PopulatesEscrowReleasedAt()
    {
        await using var ctx = BuildContext(nameof(Release_ResponseDto_PopulatesEscrowReleasedAt));
        var seed = await SeedRegistrationScenarioAsync(ctx);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.Completed;
        await ctx.SaveChangesAsync();

        var releaseResult = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.NotNull(releaseResult.Data!.EscrowReleasedAt);
        Assert.Null(releaseResult.Data.RefundedAmount);
    }

    [Fact]
    public async Task Refund_ResponseDto_PopulatesRefundFields()
    {
        await using var ctx = BuildContext(nameof(Refund_ResponseDto_PopulatesRefundFields));
        var seed = await SeedRegistrationScenarioAsync(ctx, pricePerPerson: 500m);
        var svc  = BuildService(ctx);

        var paymentId = await DepositForRegistrationAsync(svc, ctx, seed.TouristUserId, seed.RegistrationId);

        var refundResult = await svc.RefundAsync(1, paymentId, 500m, "Test");

        Assert.NotNull(refundResult.Data!.RefundedAt);
        Assert.Equal(500m, refundResult.Data.RefundedAmount);
        Assert.Null(refundResult.Data.EscrowReleasedAt);
    }
}
