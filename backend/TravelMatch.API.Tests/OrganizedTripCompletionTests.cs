using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.OrganizedTrips;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Task 1: organized-trip completion must be reachable by the owning organizer,
/// only from lifecycle states that can actually precede completion.
/// Uses EF Core InMemory — no real database required.
/// </summary>
public class OrganizedTripCompletionTests
{
    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static OrganizedTripService BuildService(ApplicationDbContext ctx) =>
        new OrganizedTripService(ctx, new RegistrationService(ctx));

    private static DateOnly PastDate(int daysAgo) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo));

    private static DateOnly FutureDate(int daysAhead) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));

    private static async Task<(int userId, int profileId)> SeedOrganizerAsync(
        ApplicationDbContext ctx, int userId, int profileId)
    {
        var user = new User
        {
            Id = userId,
            FullName = $"Org {userId}",
            Email = $"org{userId}@test.com",
            PhoneNumber = $"01700000{userId:000}",
            Role = UserRole.Organizer,
            IsActive = true
        };
        var profile = new OrganizerProfile
        {
            Id = profileId,
            UserId = userId,
            GroupOrganizationName = "Org Inc"
        };
        ctx.Users.Add(user);
        ctx.OrganizerProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return (user.Id, profile.Id);
    }

    private static async Task<OrganizedTrip> SeedTripAsync(
        ApplicationDbContext ctx,
        int tripId,
        int organizerProfileId,
        OrganizedTripStatus status,
        int? endDaysAgo = 1,
        int? deadlineDaysFromNow = null)
    {
        var trip = new OrganizedTrip
        {
            Id = tripId,
            OrganizerId = organizerProfileId,
            Title = $"Trip {tripId}",
            Destination = "Sylhet",
            Description = "Tea gardens",
            StartDate = endDaysAgo is null ? FutureDate(10) : PastDate(endDaysAgo.Value + 5),
            EndDate = endDaysAgo is null ? FutureDate(15) : PastDate(endDaysAgo.Value),
            MaxParticipants = 10,
            PricePerPerson = 300m,
            Inclusions = "Food",
            RegistrationDeadline = deadlineDaysFromNow is null
                ? PastDate(20)
                : FutureDate(deadlineDaysFromNow.Value),
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        ctx.OrganizedTrips.Add(trip);
        await ctx.SaveChangesAsync();
        return trip;
    }

    // ─── Successful completion ────────────────────────────────────────────────

    [Theory]
    [InlineData(OrganizedTripStatus.RegistrationOpen)]
    [InlineData(OrganizedTripStatus.RegistrationClosed)]
    [InlineData(OrganizedTripStatus.InProgress)]
    public async Task Complete_FromEligibleState_SetsCompleted(OrganizedTripStatus status)
    {
        await using var ctx = BuildContext($"{nameof(Complete_FromEligibleState_SetsCompleted)}_{status}");
        var (userId, profileId) = await SeedOrganizerAsync(ctx, 1, 10);
        var trip = await SeedTripAsync(ctx, 100, profileId, status);
        var svc = BuildService(ctx);

        var result = await svc.CompleteAsync(userId, trip.Id);

        Assert.True(result.Success);
        Assert.Equal(nameof(OrganizedTripStatus.Completed), result.Data!.Status);

        var saved = await ctx.OrganizedTrips.FindAsync(trip.Id);
        Assert.Equal(OrganizedTripStatus.Completed, saved!.Status);
        Assert.NotNull(saved.UpdatedAt);
    }

    // ─── Ineligible source states ─────────────────────────────────────────────

    [Theory]
    [InlineData(OrganizedTripStatus.Draft)]
    [InlineData(OrganizedTripStatus.ApplicationOpen)]
    [InlineData(OrganizedTripStatus.GuideSelection)]
    [InlineData(OrganizedTripStatus.Cancelled)]
    [InlineData(OrganizedTripStatus.Completed)]
    public async Task Complete_FromIneligibleState_IsRejected(OrganizedTripStatus status)
    {
        await using var ctx = BuildContext($"{nameof(Complete_FromIneligibleState_IsRejected)}_{status}");
        var (userId, profileId) = await SeedOrganizerAsync(ctx, 1, 10);
        var trip = await SeedTripAsync(ctx, 100, profileId, status);
        var svc = BuildService(ctx);

        var result = await svc.CompleteAsync(userId, trip.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.InvalidStatusTransition, result.Error);

        var saved = await ctx.OrganizedTrips.FindAsync(trip.Id);
        Assert.Equal(status, saved!.Status);
    }

    [Fact]
    public async Task Complete_BeforeTripEndDate_IsRejected()
    {
        await using var ctx = BuildContext(nameof(Complete_BeforeTripEndDate_IsRejected));
        var (userId, profileId) = await SeedOrganizerAsync(ctx, 1, 10);
        var trip = await SeedTripAsync(ctx, 100, profileId, OrganizedTripStatus.InProgress, endDaysAgo: null);
        var svc = BuildService(ctx);

        var result = await svc.CompleteAsync(userId, trip.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.InvalidStatusTransition, result.Error);

        var saved = await ctx.OrganizedTrips.FindAsync(trip.Id);
        Assert.Equal(OrganizedTripStatus.InProgress, saved!.Status);
    }

    // ─── Ownership ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Complete_WrongOrganizer_ReturnsUnauthorized_AndTripUnchanged()
    {
        await using var ctx = BuildContext(nameof(Complete_WrongOrganizer_ReturnsUnauthorized_AndTripUnchanged));
        var (ownerId, ownerProfileId) = await SeedOrganizerAsync(ctx, 1, 10);
        var (otherId, _) = await SeedOrganizerAsync(ctx, 2, 20);
        var trip = await SeedTripAsync(ctx, 100, ownerProfileId, OrganizedTripStatus.RegistrationOpen);
        var svc = BuildService(ctx);

        var result = await svc.CompleteAsync(otherId, trip.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.Unauthorized, result.Error);

        var saved = await ctx.OrganizedTrips.FindAsync(trip.Id);
        Assert.Equal(OrganizedTripStatus.RegistrationOpen, saved!.Status);
    }

    [Fact]
    public async Task Complete_NoOrganizerProfile_ReturnsOrganizerNotFound()
    {
        await using var ctx = BuildContext(nameof(Complete_NoOrganizerProfile_ReturnsOrganizerNotFound));
        var (ownerId, ownerProfileId) = await SeedOrganizerAsync(ctx, 1, 10);
        var trip = await SeedTripAsync(ctx, 100, ownerProfileId, OrganizedTripStatus.RegistrationOpen);
        var svc = BuildService(ctx);

        var result = await svc.CompleteAsync(ownerId + 999, trip.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.OrganizerNotFound, result.Error);
    }

    [Fact]
    public async Task Complete_TripNotFound_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(Complete_TripNotFound_ReturnsNotFound));
        var (userId, profileId) = await SeedOrganizerAsync(ctx, 1, 10);
        var svc = BuildService(ctx);

        var result = await svc.CompleteAsync(userId, 424242);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.OrganizedTripNotFound, result.Error);
    }

    // ─── Endpoint authorization ──────────────────────────────────────────────

    [Fact]
    public void CompleteEndpoint_IsOrganizerOnly()
    {
        var method = typeof(OrganizedTripsController)
            .GetMethod(nameof(OrganizedTripsController.Complete));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("{organizedTripId:int}/complete", post!.Template);

        // The role gate lives on the controller, matching publish/cancel.
        var controllerAuthorize = typeof(OrganizedTripsController)
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.NotNull(controllerAuthorize);
        Assert.Equal("Organizer", controllerAuthorize!.Roles);

        // A Tourist JWT therefore cannot reach completion at all; ownership is then
        // re-checked in the service (Complete_WrongOrganizer_ReturnsUnauthorized_AndTripUnchanged).
        var methodAuthorize = method.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.True(methodAuthorize is null || methodAuthorize.Roles == "Organizer");
    }

    // ─── Completion enables, but never performs, escrow release ──────────────

    [Fact]
    public async Task Complete_TripThenTouristRelease_Succeeds()
    {
        await using var ctx = BuildContext(nameof(Complete_TripThenTouristRelease_Succeeds));
        var (userId, profileId) = await SeedOrganizerAsync(ctx, 1, 10);

        var touristUser = new User
        {
            Id = 3, FullName = "T", Email = "t@t.com", PhoneNumber = "0171", Role = UserRole.Tourist, IsActive = true
        };
        var touristProfile = new TouristProfile { Id = 30, UserId = touristUser.Id };
        ctx.Users.Add(touristUser);
        ctx.TouristProfiles.Add(touristProfile);

        var trip = await SeedTripAsync(ctx, 100, profileId, OrganizedTripStatus.RegistrationOpen,
            endDaysAgo: 1, deadlineDaysFromNow: 5);

        var registration = new Registration
        {
            Id = 200, OrganizedTripId = trip.Id, TouristId = touristProfile.Id,
            NumberOfTravelers = 1, Status = RegistrationStatus.PendingPayment,
            TotalPrice = 300m, RegisteredAt = DateTime.UtcNow
        };
        ctx.Registrations.Add(registration);
        await ctx.SaveChangesAsync();

        var paymentService = new PaymentService(ctx, new SandboxPaymentGateway(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentService>.Instance);
        var tripService = BuildService(ctx);

        var deposit = await paymentService.PayForRegistrationAsync(touristUser.Id, registration.Id);
        Assert.True(deposit.Success);
        var paymentId = deposit.Data!.PaymentId;

        // Before completion, release is refused.
        var early = await paymentService.ReleaseEscrowAsync(touristUser.Id, paymentId);
        Assert.False(early.Success);
        Assert.Equal(PaymentStatus.Held,
            (await ctx.Payments.FindAsync(paymentId))!.Status);

        // Organizer completes the trip.
        var complete = await tripService.CompleteAsync(userId, trip.Id);
        Assert.True(complete.Success);

        // Completion itself must not have moved the funds.
        var afterCompletion = await ctx.Payments.FindAsync(paymentId);
        Assert.Equal(PaymentStatus.Held, afterCompletion!.Status);
        Assert.Null(afterCompletion.EscrowReleasedAt);

        // Now the tourist can release.
        var release = await paymentService.ReleaseEscrowAsync(touristUser.Id, paymentId);
        Assert.True(release.Success);
        Assert.Equal(PaymentStatus.Released, release.Data!.Status);
    }

    [Fact]
    public async Task Complete_WithOpenDispute_ReleaseBlocked_TripAndPaymentStatesRemainConsistent()
    {
        await using var ctx = BuildContext(nameof(Complete_WithOpenDispute_ReleaseBlocked_TripAndPaymentStatesRemainConsistent));
        var (userId, profileId) = await SeedOrganizerAsync(ctx, 1, 10);

        var touristUser = new User
        {
            Id = 3, FullName = "T", Email = "t@t.com", PhoneNumber = "0171", Role = UserRole.Tourist, IsActive = true
        };
        var touristProfile = new TouristProfile { Id = 30, UserId = touristUser.Id };
        ctx.Users.Add(touristUser);
        ctx.TouristProfiles.Add(touristProfile);

        var trip = await SeedTripAsync(ctx, 100, profileId, OrganizedTripStatus.RegistrationOpen,
            endDaysAgo: 1, deadlineDaysFromNow: 5);
        var registration = new Registration
        {
            Id = 200, OrganizedTripId = trip.Id, TouristId = touristProfile.Id,
            NumberOfTravelers = 1, Status = RegistrationStatus.PendingPayment,
            TotalPrice = 300m, RegisteredAt = DateTime.UtcNow
        };
        ctx.Registrations.Add(registration);
        await ctx.SaveChangesAsync();

        var paymentService = new PaymentService(ctx, new SandboxPaymentGateway(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentService>.Instance);
        var tripService = BuildService(ctx);

        var paymentId = (await paymentService.PayForRegistrationAsync(touristUser.Id, registration.Id))
            .Data!.PaymentId;

        Assert.True((await tripService.CompleteAsync(userId, trip.Id)).Success);

        // A dispute raised after completion freezes release until an admin resolves it.
        ctx.Disputes.Add(new Dispute
        {
            Id = 1, ReportedByUserId = touristUser.Id,
            TripType = DisputeRelatedTripType.OrganizedTrip, TripId = trip.Id,
            Reason = "Service not delivered", Description = "Hotel was not as described",
            Status = DisputeStatus.Open, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var blocked = await paymentService.ReleaseEscrowAsync(touristUser.Id, paymentId);

        Assert.False(blocked.Success);
        Assert.Equal(PaymentError.DisputeBlocksRelease, blocked.Error);

        // Neither the trip nor the payment was corrupted by the blocked release.
        var savedTrip = await ctx.OrganizedTrips.FindAsync(trip.Id);
        Assert.Equal(OrganizedTripStatus.Completed, savedTrip!.Status);

        var savedPayment = await ctx.Payments.FindAsync(paymentId);
        Assert.Equal(PaymentStatus.Held, savedPayment!.Status);
        Assert.Null(savedPayment.EscrowReleasedAt);
    }
}
