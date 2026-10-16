using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Task 4: milestone-level escrow tracking. A deposit may optionally carry a split
/// whose amounts sum to the payment amount; each milestone then holds, releases or
/// refunds independently while the parent Payment aggregate stays consistent.
/// Uses EF Core InMemory — no real database, and no real money moves.
/// </summary>
public class MilestonePaymentTests
{
    private const decimal TripPrice = 300m;

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

    private static DateOnly PastDate(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

    private record Seed(int TouristUserId, int OrganizerUserId, int TripId, int RegistrationId);

    /// <summary>
    /// Seeds organizer, tourist, an OrganizedTrip open for registration and a
    /// Registration awaiting payment.
    /// </summary>
    private static async Task<Seed> SeedAsync(ApplicationDbContext ctx)
    {
        var orgUser = new User { Id = 1, FullName = "Org", Email = "org@t.com", PhoneNumber = "01700000001", Role = UserRole.Organizer, IsActive = true };
        var orgProfile = new OrganizerProfile { Id = 10, UserId = orgUser.Id, GroupOrganizationName = "TestOrg" };
        var touristUser = new User { Id = 2, FullName = "T", Email = "t@t.com", PhoneNumber = "01700000002", Role = UserRole.Tourist, IsActive = true };
        var touristProfile = new TouristProfile { Id = 20, UserId = touristUser.Id };

        var trip = new OrganizedTrip
        {
            Id = 100, OrganizerId = orgProfile.Id,
            Title = "Milestone Trip", Destination = "Sylhet", Description = "Test",
            StartDate = PastDate(20), EndDate = PastDate(15),
            MaxParticipants = 10, PricePerPerson = TripPrice,
            Inclusions = "Food",
            RegistrationDeadline = FutureDate(5),
            Status = OrganizedTripStatus.RegistrationOpen, CreatedAt = DateTime.UtcNow
        };

        var registration = new Registration
        {
            Id = 200, OrganizedTripId = trip.Id, TouristId = touristProfile.Id,
            NumberOfTravelers = 1, Status = RegistrationStatus.PendingPayment,
            TotalPrice = TripPrice, RegisteredAt = DateTime.UtcNow
        };

        ctx.Users.AddRange(orgUser, touristUser);
        ctx.OrganizerProfiles.Add(orgProfile);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.OrganizedTrips.Add(trip);
        ctx.Registrations.Add(registration);
        await ctx.SaveChangesAsync();

        return new Seed(touristUser.Id, orgUser.Id, trip.Id, registration.Id);
    }

    /// <summary>Pays first (which requires an open trip), then completes the trip so escrow may release.</summary>
    private static async Task<int> DepositAndCompleteAsync(
        PaymentService svc, ApplicationDbContext ctx, Seed seed, int splitCount = 0)
    {
        var deposit = splitCount == 0
            ? await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId)
            : await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId,
                new PayRegistrationRequestDto { Milestones = Split(splitCount) });

        Assert.True(deposit.Success, $"Deposit failed: {deposit.Message}");

        await CompleteTripAsync(ctx, seed.TripId);
        return deposit.Data!.PaymentId;
    }

    private static async Task CompleteTripAsync(ApplicationDbContext ctx, int tripId)
    {
        var trip = await ctx.OrganizedTrips.FirstAsync(t => t.Id == tripId);
        trip.Status = OrganizedTripStatus.Completed;
        trip.EndDate = PastDate(1);
        await ctx.SaveChangesAsync();
    }

    private static List<CreatePaymentMilestoneDto> Split(int count) => count switch
    {
        2 => new List<CreatePaymentMilestoneDto>
        {
            new() { Index = 1, Label = "Advance",    Amount = 100m },
            new() { Index = 2, Label = "On arrival", Amount = 200m }
        },
        3 => new List<CreatePaymentMilestoneDto>
        {
            new() { Index = 1, Label = "Booking",    Amount = 100m },
            new() { Index = 2, Label = "Midpoint",   Amount = 100m },
            new() { Index = 3, Label = "Completion", Amount = 100m }
        },
        _ => throw new ArgumentOutOfRangeException(nameof(count))
    };

    private static async Task<PaymentResultDto> DepositAsync(
        PaymentService svc, Seed seed, List<CreatePaymentMilestoneDto> plan)
    {
        var result = await svc.PayForRegistrationAsync(
            seed.TouristUserId, seed.RegistrationId,
            new PayRegistrationRequestDto { Milestones = plan });
        Assert.True(result.Success, $"Deposit unexpectedly failed: {result.Message}");
        return result;
    }

    private static async Task<PaymentResultDto> RejectedDepositAsync(
        PaymentService svc, Seed seed, List<CreatePaymentMilestoneDto> plan)
    {
        return await svc.PayForRegistrationAsync(
            seed.TouristUserId, seed.RegistrationId,
            new PayRegistrationRequestDto { Milestones = plan });
    }

    private static void AddDispute(ApplicationDbContext ctx, int tripId, int reporterUserId,
        DisputeStatus status = DisputeStatus.Open)
    {
        ctx.Disputes.Add(new Dispute
        {
            Id = 1, ReportedByUserId = reporterUserId,
            TripType = DisputeRelatedTripType.OrganizedTrip, TripId = tripId,
            Reason = "Service not delivered", Description = "Hotel was not as described",
            Status = status, CreatedAt = DateTime.UtcNow
        });
    }

    // ─── Creation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deposit_WithMilestoneSplit_CreatesAllMilestonesHeld_PaymentStaysHeld()
    {
        await using var ctx = BuildContext(nameof(Deposit_WithMilestoneSplit_CreatesAllMilestonesHeld_PaymentStaysHeld));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);

        var result = await DepositAsync(svc, seed, Split(3));

        Assert.Equal(PaymentStatus.Held, result.Data!.Status);
        Assert.Null(result.Data.EscrowReleasedAt);
        Assert.Equal(3, result.Data.Milestones.Count);
        Assert.Equal(TripPrice, result.Data.Milestones.Sum(m => m.Amount));
        Assert.Equal(new[] { 1, 2, 3 }, result.Data.Milestones.Select(m => m.Index).OrderBy(i => i));
        Assert.All(result.Data.Milestones, m =>
        {
            Assert.Equal(PaymentMilestoneStatus.Held, m.Status);
            Assert.NotNull(m.HeldAt);
            Assert.Null(m.ReleasedAt);
            Assert.Null(m.RefundedAt);
        });

        // Persisted, not merely echoed back in the DTO.
        var inDb = await ctx.PaymentMilestones
            .Where(m => m.PaymentId == result.Data.PaymentId)
            .OrderBy(m => m.Index)
            .ToListAsync();
        Assert.Equal(3, inDb.Count);
        Assert.All(inDb, m => Assert.Equal(PaymentMilestoneStatus.Held, m.Status));
    }

    [Fact]
    public async Task Deposit_WithoutMilestoneSplit_PreservesOriginalEscrowFlow()
    {
        await using var ctx = BuildContext(nameof(Deposit_WithoutMilestoneSplit_PreservesOriginalEscrowFlow));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);

        var deposit = await svc.PayForRegistrationAsync(seed.TouristUserId, seed.RegistrationId);
        Assert.True(deposit.Success);
        Assert.Equal(PaymentStatus.Held, deposit.Data!.Status);
        Assert.Empty(deposit.Data.Milestones);
        Assert.False(await ctx.PaymentMilestones.AnyAsync());

        await CompleteTripAsync(ctx, seed.TripId);

        // A milestone-free payment still releases as a single unit.
        var release = await svc.ReleaseEscrowAsync(seed.TouristUserId, deposit.Data.PaymentId);
        Assert.True(release.Success);
        Assert.Equal(PaymentStatus.Released, release.Data!.Status);
        Assert.NotNull(release.Data.EscrowReleasedAt);
    }

    // ─── Amount validation ────────────────────────────────────────────────────

    [Fact]
    public async Task Deposit_MilestoneTotalExceedsPayment_IsRejectedAndCreatesNothing()
    {
        await using var ctx = BuildContext(nameof(Deposit_MilestoneTotalExceedsPayment_IsRejectedAndCreatesNothing));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);

        var result = await RejectedDepositAsync(svc, seed, new List<CreatePaymentMilestoneDto>
        {
            new() { Index = 1, Label = "Advance", Amount = 200m },
            new() { Index = 2, Label = "Final",   Amount = 200m }
        });

        Assert.False(result.Success);
        Assert.Equal(PaymentError.MilestoneAmountMismatch, result.Error);
        Assert.False(await ctx.Payments.AnyAsync());
        Assert.False(await ctx.PaymentMilestones.AnyAsync());
    }

    [Fact]
    public async Task Deposit_MilestoneTotalBelowPayment_IsRejected()
    {
        await using var ctx = BuildContext(nameof(Deposit_MilestoneTotalBelowPayment_IsRejected));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);

        var result = await RejectedDepositAsync(svc, seed, new List<CreatePaymentMilestoneDto>
        {
            new() { Index = 1, Label = "Advance", Amount = 100m }
        });

        Assert.False(result.Success);
        Assert.Equal(PaymentError.MilestoneAmountMismatch, result.Error);
        Assert.False(await ctx.Payments.AnyAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Deposit_MilestoneWithNonPositiveAmount_IsRejected(decimal amount)
    {
        await using var ctx = BuildContext($"{nameof(Deposit_MilestoneWithNonPositiveAmount_IsRejected)}_{amount}");
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);

        var result = await RejectedDepositAsync(svc, seed, new List<CreatePaymentMilestoneDto>
        {
            new() { Index = 1, Label = "Bad",       Amount = amount },
            new() { Index = 2, Label = "Remainder", Amount = TripPrice - amount }
        });

        Assert.False(result.Success);
        Assert.Equal(PaymentError.InvalidMilestone, result.Error);
        Assert.False(await ctx.Payments.AnyAsync());
    }

    [Fact]
    public async Task Deposit_MilestoneDuplicateIndex_IsRejected()
    {
        await using var ctx = BuildContext(nameof(Deposit_MilestoneDuplicateIndex_IsRejected));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);

        var result = await RejectedDepositAsync(svc, seed, new List<CreatePaymentMilestoneDto>
        {
            new() { Index = 1, Label = "A", Amount = 150m },
            new() { Index = 1, Label = "B", Amount = 150m }
        });

        Assert.False(result.Success);
        Assert.Equal(PaymentError.InvalidMilestone, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Deposit_MilestoneBlankLabel_IsRejected(string label)
    {
        await using var ctx = BuildContext($"{nameof(Deposit_MilestoneBlankLabel_IsRejected)}_{label.Length}");
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);

        var result = await RejectedDepositAsync(svc, seed, new List<CreatePaymentMilestoneDto>
        {
            new() { Index = 1, Label = label,   Amount = 100m },
            new() { Index = 2, Label = "Final", Amount = 200m }
        });

        Assert.False(result.Success);
        Assert.Equal(PaymentError.InvalidMilestone, result.Error);
    }

    // ─── Individual milestone release ─────────────────────────────────────────

    [Fact]
    public async Task ReleaseMilestone_OneOfThree_ReleasesOnlyThatMilestone_ParentStaysHeld()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_OneOfThree_ReleasesOnlyThatMilestone_ParentStaysHeld));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(3));
        var paymentId = deposit.Data!.PaymentId;
        var first = deposit.Data.Milestones.Single(m => m.Index == 1);

        await CompleteTripAsync(ctx, seed.TripId);

        var release = await svc.ReleaseMilestoneAsync(seed.TouristUserId, paymentId, first.MilestoneId);

        Assert.True(release.Success);
        Assert.Equal(PaymentStatus.Held, release.Data!.Status);
        Assert.Null(release.Data.EscrowReleasedAt);
        Assert.Equal(PaymentMilestoneStatus.Released,
            release.Data.Milestones.Single(m => m.Index == 1).Status);
        Assert.Equal(PaymentMilestoneStatus.Held,
            release.Data.Milestones.Single(m => m.Index == 2).Status);

        var persisted = await ctx.Payments.Include(p => p.Milestones)
            .FirstAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentStatus.Held, persisted.Status);
        Assert.Single(persisted.Milestones.Where(m => m.Status == PaymentMilestoneStatus.Released));
        Assert.NotNull(persisted.Milestones.Single(m => m.Index == 1).ReleasedAt);
    }

    [Fact]
    public async Task ReleaseMilestone_ReleasingAll_ParentBecomesReleased()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_ReleasingAll_ParentBecomesReleased));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(2));
        var paymentId = deposit.Data!.PaymentId;

        await CompleteTripAsync(ctx, seed.TripId);

        foreach (var ms in deposit.Data.Milestones)
        {
            var step = await svc.ReleaseMilestoneAsync(seed.TouristUserId, paymentId, ms.MilestoneId);
            Assert.True(step.Success, step.Message);
        }

        var final = await ctx.Payments.Include(p => p.Milestones)
            .FirstAsync(p => p.Id == paymentId);

        Assert.Equal(PaymentStatus.Released, final.Status);
        Assert.NotNull(final.EscrowReleasedAt);
        Assert.All(final.Milestones, m =>
        {
            Assert.Equal(PaymentMilestoneStatus.Released, m.Status);
            Assert.NotNull(m.ReleasedAt);
        });
    }

    [Fact]
    public async Task ReleaseMilestone_Twice_ReturnsAlreadySettled_AndDoesNotDoublePay()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_Twice_ReturnsAlreadySettled_AndDoesNotDoublePay));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(2));
        var paymentId = deposit.Data!.PaymentId;
        var ms = deposit.Data.Milestones.Single(m => m.Index == 1);

        await CompleteTripAsync(ctx, seed.TripId);

        var first = await svc.ReleaseMilestoneAsync(seed.TouristUserId, paymentId, ms.MilestoneId);
        Assert.True(first.Success);
        var releasedAt = first.Data!.Milestones.Single(m => m.Index == 1).ReleasedAt;
        Assert.NotNull(releasedAt);

        var second = await svc.ReleaseMilestoneAsync(seed.TouristUserId, paymentId, ms.MilestoneId);

        Assert.False(second.Success);
        Assert.Equal(PaymentError.MilestoneAlreadySettled, second.Error);

        // The rejected repeat left the milestone exactly as it was.
        var persisted = await ctx.PaymentMilestones.FirstAsync(m => m.Id == ms.MilestoneId);
        Assert.Equal(PaymentMilestoneStatus.Released, persisted.Status);
        Assert.Equal(releasedAt, persisted.ReleasedAt);
    }

    [Fact]
    public async Task ReleaseMilestone_TripNotCompleted_ReturnsTripNotCompleted()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_TripNotCompleted_ReturnsTripNotCompleted));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(2));

        // Trip deliberately left in RegistrationOpen.
        var release = await svc.ReleaseMilestoneAsync(
            seed.TouristUserId, deposit.Data!.PaymentId,
            deposit.Data.Milestones.Single(m => m.Index == 1).MilestoneId);

        Assert.False(release.Success);
        Assert.Equal(PaymentError.TripNotCompleted, release.Error);
        Assert.False(await ctx.PaymentMilestones.AnyAsync(m =>
            m.Status == PaymentMilestoneStatus.Released));
    }

    [Fact]
    public async Task ReleaseMilestone_OpenDispute_ReturnsDisputeBlocksRelease()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_OpenDispute_ReturnsDisputeBlocksRelease));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(2));

        await CompleteTripAsync(ctx, seed.TripId);
        AddDispute(ctx, seed.TripId, seed.TouristUserId);
        await ctx.SaveChangesAsync();

        var release = await svc.ReleaseMilestoneAsync(
            seed.TouristUserId, deposit.Data!.PaymentId,
            deposit.Data.Milestones.Single(m => m.Index == 1).MilestoneId);

        Assert.False(release.Success);
        Assert.Equal(PaymentError.DisputeBlocksRelease, release.Error);

        var persisted = await ctx.Payments.Include(p => p.Milestones)
            .FirstAsync(p => p.Id == deposit.Data.PaymentId);
        Assert.Equal(PaymentStatus.Held, persisted.Status);
        Assert.All(persisted.Milestones, m => Assert.Equal(PaymentMilestoneStatus.Held, m.Status));
    }

    [Fact]
    public async Task ReleaseMilestone_WrongTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_WrongTourist_ReturnsUnauthorized));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(2));
        var milestoneId = deposit.Data!.Milestones.Single(m => m.Index == 1).MilestoneId;

        await CompleteTripAsync(ctx, seed.TripId);

        var release = await svc.ReleaseMilestoneAsync(seed.TouristUserId + 77, deposit.Data.PaymentId, milestoneId);

        Assert.False(release.Success);
        Assert.Equal(PaymentError.Unauthorized, release.Error);
    }

    [Fact]
    public async Task ReleaseMilestone_UnknownMilestoneId_ReturnsMilestoneNotFound()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_UnknownMilestoneId_ReturnsMilestoneNotFound));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(2));

        await CompleteTripAsync(ctx, seed.TripId);

        var release = await svc.ReleaseMilestoneAsync(seed.TouristUserId, deposit.Data!.PaymentId, 999_999);

        Assert.False(release.Success);
        Assert.Equal(PaymentError.MilestoneNotFound, release.Error);
    }

    [Fact]
    public async Task ReleaseMilestone_MilestoneBelongingToAnotherPayment_ReturnsMilestoneNotFound()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_MilestoneBelongingToAnotherPayment_ReturnsMilestoneNotFound));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(2));

        // A milestone that exists in the table but is not on this payment.
        var otherMilestone = new PaymentMilestone
        {
            PaymentId = 4242, Index = 1, Label = "Other", Amount = 10m,
            Status = PaymentMilestoneStatus.Held,
            CreatedAt = DateTime.UtcNow, HeldAt = DateTime.UtcNow
        };
        ctx.PaymentMilestones.Add(otherMilestone);
        await ctx.SaveChangesAsync();

        await CompleteTripAsync(ctx, seed.TripId);

        var release = await svc.ReleaseMilestoneAsync(
            seed.TouristUserId, deposit.Data!.PaymentId, otherMilestone.Id);

        Assert.False(release.Success);
        Assert.Equal(PaymentError.MilestoneNotFound, release.Error);
    }

    [Fact]
    public async Task ReleaseMilestone_AfterParentAlreadyReleased_ReturnsEscrowAlreadySettled()
    {
        await using var ctx = BuildContext(nameof(ReleaseMilestone_AfterParentAlreadyReleased_ReturnsEscrowAlreadySettled));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(2));
        var paymentId = deposit.Data!.PaymentId;
        var milestoneId = deposit.Data.Milestones.Single(m => m.Index == 1).MilestoneId;

        await CompleteTripAsync(ctx, seed.TripId);

        var parent = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);
        Assert.True(parent.Success);

        var milestone = await svc.ReleaseMilestoneAsync(seed.TouristUserId, paymentId, milestoneId);

        Assert.False(milestone.Success);
        Assert.Equal(PaymentError.EscrowAlreadySettled, milestone.Error);
    }

    // ─── Parent release covers milestones ─────────────────────────────────────

    [Fact]
    public async Task ReleaseEscrow_WithMilestones_ReleasesEveryHeldMilestoneAndParent()
    {
        await using var ctx = BuildContext(nameof(ReleaseEscrow_WithMilestones_ReleasesEveryHeldMilestoneAndParent));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(3));
        var paymentId = deposit.Data!.PaymentId;

        await CompleteTripAsync(ctx, seed.TripId);

        // Release one milestone, then settle the rest through the parent endpoint.
        await svc.ReleaseMilestoneAsync(seed.TouristUserId, paymentId,
            deposit.Data.Milestones.Single(m => m.Index == 1).MilestoneId);

        var release = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.True(release.Success);
        Assert.Equal(PaymentStatus.Released, release.Data!.Status);
        Assert.NotNull(release.Data.EscrowReleasedAt);
        Assert.All(release.Data.Milestones, m => Assert.Equal(PaymentMilestoneStatus.Released, m.Status));
        Assert.Equal(TripPrice, release.Data.Milestones.Sum(m => m.Amount));
    }

    [Fact]
    public async Task ReleaseEscrow_WithOpenDispute_LeavesMilestonesHeld()
    {
        await using var ctx = BuildContext(nameof(ReleaseEscrow_WithOpenDispute_LeavesMilestonesHeld));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var paymentId = await DepositAndCompleteAsync(svc, ctx, seed, 2);
        AddDispute(ctx, seed.TripId, seed.TouristUserId);
        await ctx.SaveChangesAsync();

        var release = await svc.ReleaseEscrowAsync(seed.TouristUserId, paymentId);

        Assert.False(release.Success);
        Assert.Equal(PaymentError.DisputeBlocksRelease, release.Error);

        var persisted = await ctx.Payments.Include(p => p.Milestones)
            .FirstAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentStatus.Held, persisted.Status);
        Assert.All(persisted.Milestones, m => Assert.Equal(PaymentMilestoneStatus.Held, m.Status));
    }

    // ─── Refund interaction ───────────────────────────────────────────────────

    [Fact]
    public async Task Refund_AfterPartialMilestoneRelease_IsCappedAtUnreleasedBalance()
    {
        await using var ctx = BuildContext(nameof(Refund_AfterPartialMilestoneRelease_IsCappedAtUnreleasedBalance));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var paymentId = await DepositAndCompleteAsync(svc, ctx, seed, 2);

        var held = await ctx.PaymentMilestones
            .Where(m => m.PaymentId == paymentId && m.Index == 1)
            .FirstAsync();
        await svc.ReleaseMilestoneAsync(seed.TouristUserId, paymentId, held.Id); // 100 of 300 released

        var tooMuch = await svc.RefundAsync(1, paymentId, TripPrice, "Full refund attempt");
        Assert.False(tooMuch.Success);
        Assert.Equal(PaymentError.RefundExceedsHeldAmount, tooMuch.Error);

        var capped = await svc.RefundAsync(1, paymentId, null, "Return the remainder");
        Assert.True(capped.Success);
        Assert.Equal(200m, capped.Data!.RefundedAmount);
        Assert.Equal(PaymentStatus.PartiallyRefunded, capped.Data.Status);

        var persisted = await ctx.Payments.Include(p => p.Milestones)
            .FirstAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentMilestoneStatus.Released,
            persisted.Milestones.Single(m => m.Index == 1).Status);
        Assert.Equal(PaymentMilestoneStatus.Refunded,
            persisted.Milestones.Single(m => m.Index == 2).Status);
        Assert.NotNull(persisted.Milestones.Single(m => m.Index == 2).RefundedAt);
    }

    [Fact]
    public async Task Refund_FullBeforeAnyRelease_FlipsAllMilestonesToRefunded()
    {
        await using var ctx = BuildContext(nameof(Refund_FullBeforeAnyRelease_FlipsAllMilestonesToRefunded));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var deposit = await DepositAsync(svc, seed, Split(3));

        var refund = await svc.RefundAsync(1, deposit.Data!.PaymentId, null, "Trip cancelled");

        Assert.True(refund.Success);
        Assert.Equal(PaymentStatus.Refunded, refund.Data!.Status);
        Assert.Equal(TripPrice, refund.Data.RefundedAmount);
        Assert.All(refund.Data.Milestones, m =>
        {
            Assert.Equal(PaymentMilestoneStatus.Refunded, m.Status);
            Assert.NotNull(m.RefundedAt);
        });

        // A fully refunded payment cannot have milestones released afterwards.
        var attempt = await svc.ReleaseMilestoneAsync(seed.TouristUserId, deposit.Data.PaymentId,
            deposit.Data.Milestones.Single(m => m.Index == 1).MilestoneId);
        Assert.False(attempt.Success);
        Assert.Equal(PaymentError.EscrowAlreadySettled, attempt.Error);
    }

    [Fact]
    public async Task Refund_BlockedByOpenDispute_MilestonesRemainHeld()
    {
        await using var ctx = BuildContext(nameof(Refund_BlockedByOpenDispute_MilestonesRemainHeld));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        var paymentId = await DepositAndCompleteAsync(svc, ctx, seed, 2);
        AddDispute(ctx, seed.TripId, seed.TouristUserId);
        await ctx.SaveChangesAsync();

        var refund = await svc.RefundAsync(1, paymentId, 100m, "Try while disputed");

        Assert.False(refund.Success);
        Assert.Equal(PaymentError.DisputeResolutionRequired, refund.Error);

        var persisted = await ctx.Payments.Include(p => p.Milestones)
            .FirstAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentStatus.Held, persisted.Status);
        Assert.Equal(0m, persisted.RefundedAmount ?? 0m);
        Assert.All(persisted.Milestones, m => Assert.Equal(PaymentMilestoneStatus.Held, m.Status));
    }

    // ─── Read paths expose milestones ─────────────────────────────────────────

    [Fact]
    public async Task GetMyPayments_Tourist_SeesMilestones()
    {
        await using var ctx = BuildContext(nameof(GetMyPayments_Tourist_SeesMilestones));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        await DepositAsync(svc, seed, Split(2));

        var list = await svc.GetMyPaymentsAsync(seed.TouristUserId);

        Assert.True(list.Success);
        Assert.Single(list.Data);
        Assert.Equal(2, list.Data[0].Milestones.Count);
        Assert.Equal(100m, list.Data[0].Milestones.Single(m => m.Index == 1).Amount);
    }

    [Fact]
    public async Task GetTripPayments_Organizer_SeesMilestonesForOwnTrip()
    {
        await using var ctx = BuildContext(nameof(GetTripPayments_Organizer_SeesMilestonesForOwnTrip));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        await DepositAsync(svc, seed, Split(3));

        var list = await svc.GetTripPaymentsAsync(seed.OrganizerUserId, seed.TripId);

        Assert.True(list.Success);
        Assert.Single(list.Data);
        Assert.Equal(3, list.Data[0].Milestones.Count);
        Assert.All(list.Data[0].Milestones, m =>
            Assert.Equal(PaymentMilestoneStatus.Held, m.Status));
    }

    [Fact]
    public async Task GetPaymentsByUser_Admin_SeesMilestones()
    {
        await using var ctx = BuildContext(nameof(GetPaymentsByUser_Admin_SeesMilestones));
        var svc = BuildService(ctx);
        var seed = await SeedAsync(ctx);
        await DepositAsync(svc, seed, Split(2));

        var list = await svc.GetPaymentsByUserAsync(seed.TouristUserId);

        Assert.True(list.Success);
        Assert.Equal(2, list.Data.Single().Milestones.Count);
    }
}
