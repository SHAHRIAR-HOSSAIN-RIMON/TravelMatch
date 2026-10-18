using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Registrations;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for RegistrationService: register, confirm payment, cancel (tourist),
/// cancel (organizer), capacity enforcement, duplicate prevention, deadline enforcement,
/// and authorization checks.
/// Uses EF Core InMemory so no real database is needed.
/// </summary>
public class RegistrationServiceTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static RegistrationService BuildService(ApplicationDbContext ctx) =>
        new RegistrationService(ctx);

    private static DateOnly FutureDate(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days));

    /// <summary>
    /// Seeds a complete scenario: organizer, tourist user + profile,
    /// and an OrganizedTrip in RegistrationOpen status.
    /// Returns the tourist user Id and the trip Id.
    /// </summary>
    private static async Task<SeedResult> SeedScenarioAsync(
        ApplicationDbContext ctx,
        OrganizedTripStatus tripStatus = OrganizedTripStatus.RegistrationOpen,
        int maxParticipants = 10,
        int deadlineDaysFromNow = 5,
        int existingConfirmedSpots = 0,   // adds Confirmed registrations from other tourists
        int existingPendingSpots = 0,     // adds PendingPayment registrations from other tourists
        bool activeTourist = true,
        UserRole touristRole = UserRole.Tourist,
        bool hasTouristProfile = true)
    {
        // Organizer
        var orgUser = new User
        {
            Id = 10,
            FullName = "Org User",
            Email = "org@test.com",
            PhoneNumber = "01700000010",
            Role = UserRole.Organizer,
            IsActive = true
        };
        var orgProfile = new OrganizerProfile
        {
            Id = 100,
            UserId = orgUser.Id,
            GroupOrganizationName = "Test Org"
        };

        // Tourist
        var touristUser = new User
        {
            Id = 20,
            FullName = "Tourist User",
            Email = "tourist@test.com",
            PhoneNumber = "01700000020",
            Role = touristRole,
            IsActive = activeTourist
        };
        TouristProfile? touristProfile = hasTouristProfile
            ? new TouristProfile { Id = 200, UserId = touristUser.Id }
            : null;

        // Trip
        var trip = new OrganizedTrip
        {
            Id = 500,
            OrganizerId = orgProfile.Id,
            Title = "Test Trek",
            Destination = "Bandarban",
            Description = "Scenic hills",
            StartDate = FutureDate(20),
            EndDate = FutureDate(25),
            MaxParticipants = maxParticipants,
            PricePerPerson = 400m,
            Inclusions = "Food",
            RegistrationDeadline = FutureDate(deadlineDaysFromNow),
            Status = tripStatus,
            CreatedAt = DateTime.UtcNow
        };

        ctx.Users.Add(orgUser);
        ctx.OrganizerProfiles.Add(orgProfile);
        ctx.Users.Add(touristUser);
        if (touristProfile is not null) ctx.TouristProfiles.Add(touristProfile);
        ctx.OrganizedTrips.Add(trip);

        // Extra confirmed registrations from other tourists
        for (int i = 0; i < existingConfirmedSpots; i++)
        {
            var u = new User { Id = 1000 + i, FullName = $"C{i}", Email = $"c{i}@t.com", PhoneNumber = "017", Role = UserRole.Tourist, IsActive = true };
            var p = new TouristProfile { Id = 2000 + i, UserId = u.Id };
            var r = new Registration { Id = 3000 + i, OrganizedTripId = trip.Id, TouristId = p.Id, NumberOfTravelers = 1, Status = RegistrationStatus.Confirmed, RegisteredAt = DateTime.UtcNow };
            ctx.Users.Add(u);
            ctx.TouristProfiles.Add(p);
            ctx.Registrations.Add(r);
        }

        // Extra pending registrations from other tourists
        for (int i = 0; i < existingPendingSpots; i++)
        {
            var u = new User { Id = 4000 + i, FullName = $"P{i}", Email = $"p{i}@t.com", PhoneNumber = "018", Role = UserRole.Tourist, IsActive = true };
            var p = new TouristProfile { Id = 5000 + i, UserId = u.Id };
            var r = new Registration { Id = 6000 + i, OrganizedTripId = trip.Id, TouristId = p.Id, NumberOfTravelers = 1, Status = RegistrationStatus.PendingPayment, RegisteredAt = DateTime.UtcNow };
            ctx.Users.Add(u);
            ctx.TouristProfiles.Add(p);
            ctx.Registrations.Add(r);
        }

        await ctx.SaveChangesAsync();

        return new SeedResult
        {
            TouristUserId = touristUser.Id,
            TouristProfileId = touristProfile?.Id ?? 0,
            TripId = trip.Id,
            OrganizerUserId = orgUser.Id,
            OrganizerProfileId = orgProfile.Id
        };
    }

    private record SeedResult
    {
        public int TouristUserId { get; init; }
        public int TouristProfileId { get; init; }
        public int TripId { get; init; }
        public int OrganizerUserId { get; init; }
        public int OrganizerProfileId { get; init; }
    }

    // ─── RegisterAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task Register_Success_CreatesPendingPaymentRegistration()
    {
        await using var ctx = BuildContext(nameof(Register_Success_CreatesPendingPaymentRegistration));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 2 });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("PendingPayment", result.Data!.Status);
        Assert.Equal(2, result.Data.NumberOfTravelers);
        Assert.Equal(800m, result.Data.TotalPrice); // 400 * 2
    }

    [Fact]
    public async Task Register_InvalidNumberOfTravelers_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(Register_InvalidNumberOfTravelers_ReturnsError));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 0 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.InvalidNumberOfTravelers, result.Error);
    }

    [Fact]
    public async Task Register_TripNotOpenForRegistration_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(Register_TripNotOpenForRegistration_ReturnsError));
        var seed = await SeedScenarioAsync(ctx, tripStatus: OrganizedTripStatus.Draft);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.TripNotOpenForRegistration, result.Error);
    }

    [Fact]
    public async Task Register_CancelledTrip_ReturnsTripCancelled()
    {
        await using var ctx = BuildContext(nameof(Register_CancelledTrip_ReturnsTripCancelled));
        var seed = await SeedScenarioAsync(ctx, tripStatus: OrganizedTripStatus.Cancelled);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.TripCancelled, result.Error);
    }

    [Fact]
    public async Task Register_TripNotFound_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(Register_TripNotFound_ReturnsNotFound));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, 9999,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.OrganizedTripNotFound, result.Error);
    }

    [Fact]
    public async Task Register_DeadlinePassed_ReturnsDeadlinePassed()
    {
        await using var ctx = BuildContext(nameof(Register_DeadlinePassed_ReturnsDeadlinePassed));
        var seed = await SeedScenarioAsync(ctx, deadlineDaysFromNow: -1);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.DeadlinePassed, result.Error);
    }

    [Fact]
    public async Task Register_DuplicateRegistration_ReturnsConflict()
    {
        await using var ctx = BuildContext(nameof(Register_DuplicateRegistration_ReturnsConflict));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        // First registration succeeds
        var first = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(first.Success);

        // Second registration for same tourist + trip must fail
        var second = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(second.Success);
        Assert.Equal(RegistrationError.DuplicateRegistration, second.Error);
    }

    [Fact]
    public async Task Register_CancelledRegistration_BlocksRereregistration()
    {
        await using var ctx = BuildContext(nameof(Register_CancelledRegistration_BlocksRereregistration));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        // Register, then cancel
        var first = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(first.Success);
        await svc.CancelRegistrationAsync(seed.TouristUserId, seed.TripId, first.Data!.Id, "changed mind");

        // Re-register attempt must be blocked
        var second = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(second.Success);
        Assert.Equal(RegistrationError.DuplicateRegistration, second.Error);
    }

    [Fact]
    public async Task Register_TripFull_ReturnsTripFull()
    {
        await using var ctx = BuildContext(nameof(Register_TripFull_ReturnsTripFull));
        // All 3 spots taken by confirmed registrations
        var seed = await SeedScenarioAsync(ctx, maxParticipants: 3, existingConfirmedSpots: 3);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.TripFull, result.Error);
    }

    [Fact]
    public async Task Register_PendingPaymentCountsAgainstCapacity()
    {
        await using var ctx = BuildContext(nameof(Register_PendingPaymentCountsAgainstCapacity));
        // 3 spots, 3 taken by pending-payment registrations — still full
        var seed = await SeedScenarioAsync(ctx, maxParticipants: 3, existingPendingSpots: 3);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.TripFull, result.Error);
    }

    [Fact]
    public async Task Register_ExceedsRemainingSpots_ReturnsExceedsCapacity()
    {
        await using var ctx = BuildContext(nameof(Register_ExceedsRemainingSpots_ReturnsExceedsCapacity));
        // 3 spots, 2 taken → 1 remaining; requesting 2
        var seed = await SeedScenarioAsync(ctx, maxParticipants: 3, existingConfirmedSpots: 2);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 2 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.ExceedsCapacity, result.Error);
    }

    [Fact]
    public async Task Register_InactiveTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Register_InactiveTourist_ReturnsUnauthorized));
        var seed = await SeedScenarioAsync(ctx, activeTourist: false);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task Register_WrongRole_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Register_WrongRole_ReturnsUnauthorized));
        var seed = await SeedScenarioAsync(ctx, touristRole: UserRole.Guide);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task Register_NoTouristProfile_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Register_NoTouristProfile_ReturnsUnauthorized));
        var seed = await SeedScenarioAsync(ctx, hasTouristProfile: false);
        var svc = BuildService(ctx);

        var result = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.Unauthorized, result.Error);
    }

    // ─── ConfirmPaymentAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPayment_Success_SetsStatusConfirmed()
    {
        await using var ctx = BuildContext(nameof(ConfirmPayment_Success_SetsStatusConfirmed));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        var result = await svc.ConfirmPaymentAsync(seed.TouristUserId, registered.Data!.Id);

        Assert.True(result.Success);
        Assert.Equal("Confirmed", result.Data!.Status);
        Assert.NotNull(result.Data.ConfirmedAt);
    }

    [Fact]
    public async Task ConfirmPayment_AlreadyConfirmed_ReturnsDuplicateError()
    {
        await using var ctx = BuildContext(nameof(ConfirmPayment_AlreadyConfirmed_ReturnsDuplicateError));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        await svc.ConfirmPaymentAsync(seed.TouristUserId, registered.Data!.Id);

        // Second confirm must fail
        var result = await svc.ConfirmPaymentAsync(seed.TouristUserId, registered.Data.Id);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.DuplicateRegistration, result.Error);
    }

    [Fact]
    public async Task ConfirmPayment_RegistrationNotFound_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(ConfirmPayment_RegistrationNotFound_ReturnsNotFound));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.ConfirmPaymentAsync(seed.TouristUserId, 9999);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.RegistrationNotFound, result.Error);
    }

    [Fact]
    public async Task ConfirmPayment_WrongTourist_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(ConfirmPayment_WrongTourist_ReturnsNotFound));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        // Seed a second tourist with their own profile
        var otherUser = new User { Id = 99, FullName = "Other", Email = "o@t.com", PhoneNumber = "019", Role = UserRole.Tourist, IsActive = true };
        var otherProfile = new TouristProfile { Id = 990, UserId = otherUser.Id };
        ctx.Users.Add(otherUser);
        ctx.TouristProfiles.Add(otherProfile);
        await ctx.SaveChangesAsync();

        var result = await svc.ConfirmPaymentAsync(otherUser.Id, registered.Data!.Id);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.RegistrationNotFound, result.Error);
    }

    [Fact]
    public async Task ConfirmPayment_DeadlinePassed_ReturnsDeadlinePassed()
    {
        await using var ctx = BuildContext(nameof(ConfirmPayment_DeadlinePassed_ReturnsDeadlinePassed));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        // Retroactively push deadline into the past
        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.RegistrationDeadline = FutureDate(-1);
        await ctx.SaveChangesAsync();

        var result = await svc.ConfirmPaymentAsync(seed.TouristUserId, registered.Data!.Id);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.DeadlinePassed, result.Error);
    }

    [Fact]
    public async Task ConfirmPayment_CapacityGoneByTheTime_ReturnsExceedsCapacity()
    {
        await using var ctx = BuildContext(nameof(ConfirmPayment_CapacityGoneByTheTime_ReturnsExceedsCapacity));
        // 2 max; tourist holds the only pending spot; we'll fill the other with confirmed
        var seed = await SeedScenarioAsync(ctx, maxParticipants: 1);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        // Another tourist races in and grabs the spot directly in the DB
        var otherUser = new User { Id = 77, FullName = "Racer", Email = "r@t.com", PhoneNumber = "017", Role = UserRole.Tourist, IsActive = true };
        var otherProfile = new TouristProfile { Id = 770, UserId = otherUser.Id };
        var otherReg = new Registration
        {
            Id = 9900,
            OrganizedTripId = seed.TripId,
            TouristId = otherProfile.Id,
            NumberOfTravelers = 1,
            Status = RegistrationStatus.Confirmed,
            RegisteredAt = DateTime.UtcNow
        };
        ctx.Users.Add(otherUser);
        ctx.TouristProfiles.Add(otherProfile);
        ctx.Registrations.Add(otherReg);
        await ctx.SaveChangesAsync();

        var result = await svc.ConfirmPaymentAsync(seed.TouristUserId, registered.Data!.Id);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.ExceedsCapacity, result.Error);
    }

    // ─── CancelRegistrationAsync (tourist) ──────────────────────────────────

    [Fact]
    public async Task CancelRegistration_Tourist_Success_SetsCancelled()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Tourist_Success_SetsCancelled));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        var result = await svc.CancelRegistrationAsync(
            seed.TouristUserId, seed.TripId, registered.Data!.Id, "changed plans");

        Assert.True(result.Success);
        Assert.Equal("Cancelled", result.Data!.Status);
        Assert.Equal("changed plans", result.Data.CancellationReason);
        Assert.NotNull(result.Data.CancelledAt);
    }

    [Fact]
    public async Task CancelRegistration_Tourist_AlreadyCancelled_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Tourist_AlreadyCancelled_ReturnsError));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        await svc.CancelRegistrationAsync(seed.TouristUserId, seed.TripId, registered.Data!.Id, null);

        var result = await svc.CancelRegistrationAsync(
            seed.TouristUserId, seed.TripId, registered.Data.Id, null);

        Assert.False(result.Success);
        // CancelCoreAsync maps "already cancelled" to TripNotOpenForRegistration
        Assert.Equal(RegistrationError.TripNotOpenForRegistration, result.Error);
    }

    [Fact]
    public async Task CancelRegistration_Tourist_WrongOwner_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Tourist_WrongOwner_ReturnsNotFound));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        var otherUser = new User { Id = 55, FullName = "Other", Email = "oth@t.com", PhoneNumber = "019", Role = UserRole.Tourist, IsActive = true };
        var otherProfile = new TouristProfile { Id = 550, UserId = otherUser.Id };
        ctx.Users.Add(otherUser);
        ctx.TouristProfiles.Add(otherProfile);
        await ctx.SaveChangesAsync();

        var result = await svc.CancelRegistrationAsync(
            otherUser.Id, seed.TripId, registered.Data!.Id, null);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.RegistrationNotFound, result.Error);
    }

    [Fact]
    public async Task CancelRegistration_Tourist_TripInProgress_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Tourist_TripInProgress_ReturnsError));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        // Simulate trip starting
        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.InProgress;
        await ctx.SaveChangesAsync();

        var result = await svc.CancelRegistrationAsync(
            seed.TouristUserId, seed.TripId, registered.Data!.Id, null);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.TripNotOpenForRegistration, result.Error);
    }

    // ─── CancelAsOrganizerAsync ──────────────────────────────────────────────

    [Fact]
    public async Task CancelRegistration_Organizer_Success_SetsCancelled()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Organizer_Success_SetsCancelled));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        var result = await svc.CancelAsOrganizerAsync(
            seed.OrganizerUserId, seed.TripId, registered.Data!.Id, "no-show risk");

        Assert.True(result.Success);
        Assert.Equal("Cancelled", result.Data!.Status);
        Assert.Equal("no-show risk", result.Data.CancellationReason);
    }

    [Fact]
    public async Task CancelRegistration_Organizer_WrongOrganizer_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Organizer_WrongOrganizer_ReturnsUnauthorized));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        // Seed a second organizer who does NOT own the trip
        var otherOrgUser = new User { Id = 44, FullName = "Other Org", Email = "o2@t.com", PhoneNumber = "017", Role = UserRole.Organizer, IsActive = true };
        var otherOrgProfile = new OrganizerProfile { Id = 440, UserId = otherOrgUser.Id, GroupOrganizationName = "Other Org" };
        ctx.Users.Add(otherOrgUser);
        ctx.OrganizerProfiles.Add(otherOrgProfile);
        await ctx.SaveChangesAsync();

        var result = await svc.CancelAsOrganizerAsync(
            otherOrgUser.Id, seed.TripId, registered.Data!.Id, null);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task CancelRegistration_Organizer_RegistrationNotFound_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Organizer_RegistrationNotFound_ReturnsNotFound));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.CancelAsOrganizerAsync(
            seed.OrganizerUserId, seed.TripId, 9999, null);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.RegistrationNotFound, result.Error);
    }

    [Fact]
    public async Task CancelRegistration_Organizer_TripCompleted_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Organizer_TripCompleted_ReturnsError));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.True(registered.Success);

        var trip = await ctx.OrganizedTrips.FindAsync(seed.TripId);
        trip!.Status = OrganizedTripStatus.Completed;
        await ctx.SaveChangesAsync();

        var result = await svc.CancelAsOrganizerAsync(
            seed.OrganizerUserId, seed.TripId, registered.Data!.Id, null);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.TripNotOpenForRegistration, result.Error);
    }

    [Fact]
    public async Task CancelRegistration_Organizer_NoOrganizerProfile_ReturnsOrganizerNotFound()
    {
        await using var ctx = BuildContext(nameof(CancelRegistration_Organizer_NoOrganizerProfile_ReturnsOrganizerNotFound));
        var seed = await SeedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var registered = await svc.RegisterAsync(seed.TouristUserId, seed.TripId,
            new CreateRegistrationDto { NumberOfTravelers = 1 });

        // userId 9999 has no organizer profile
        var result = await svc.CancelAsOrganizerAsync(9999, seed.TripId, registered.Data!.Id, null);

        Assert.False(result.Success);
        Assert.Equal(RegistrationError.OrganizerNotFound, result.Error);
    }
}
