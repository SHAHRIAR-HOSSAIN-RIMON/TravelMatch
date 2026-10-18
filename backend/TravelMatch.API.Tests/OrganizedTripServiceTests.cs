using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.OrganizedTrips;
using TravelMatch.API.DTOs.Registrations;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for OrganizedTripService: create, publish, update, cancel,
/// get my trips, get available trips (public browse), and get available trip detail.
/// Uses EF Core InMemory so no real database is needed.
/// </summary>
public class OrganizedTripServiceTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static OrganizedTripService BuildService(ApplicationDbContext ctx)
    {
        var registrationService = new RegistrationService(ctx);
        return new OrganizedTripService(ctx, registrationService);
    }

    private static DateOnly FutureDate(int daysFromNow) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow));

    /// <summary>Seeds an organizer user + profile and returns the User.Id.</summary>
    private static async Task<(int userId, int profileId)> SeedOrganizerAsync(
        ApplicationDbContext ctx, int userId = 1)
    {
        var user = new User
        {
            Id = userId,
            FullName = "Alice Organizer",
            Email = $"org{userId}@test.com",
            PhoneNumber = "01700000001",
            PasswordHash = "hash",
            Role = UserRole.Organizer,
            IsActive = true
        };
        var profile = new OrganizerProfile
        {
            Id = userId * 100,
            UserId = userId,
            GroupOrganizationName = "Test Tours"
        };
        ctx.Users.Add(user);
        ctx.OrganizerProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return (user.Id, profile.Id);
    }

    /// <summary>Creates a valid CreateOrganizedTripDto with all required fields.</summary>
    private static CreateOrganizedTripDto ValidDto(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        DateOnly? deadline = null,
        int maxParticipants = 10,
        decimal pricePerPerson = 500m)
    {
        var start = startDate ?? FutureDate(30);
        var end = endDate ?? start.AddDays(5);
        var dl = deadline ?? start.AddDays(-2);
        return new CreateOrganizedTripDto
        {
            Title = "Bandarban Trek",
            Destination = "Bandarban",
            Description = "A scenic hiking tour.",
            StartDate = start,
            EndDate = end,
            MaxParticipants = maxParticipants,
            PricePerPerson = pricePerPerson,
            Inclusions = "Food, accommodation",
            Exclusions = null,
            RegistrationDeadline = dl
        };
    }

    // ─── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Success_ReturnsDraftTrip()
    {
        await using var ctx = BuildContext(nameof(Create_Success_ReturnsDraftTrip));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.CreateAsync(userId, ValidDto());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Draft", result.Data!.Status);
        Assert.Equal("Bandarban Trek", result.Data.Title);
    }

    [Fact]
    public async Task Create_MissingTitle_ReturnsValidationError()
    {
        await using var ctx = BuildContext(nameof(Create_MissingTitle_ReturnsValidationError));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var dto = ValidDto();
        dto.Title = "   ";

        var result = await svc.CreateAsync(userId, dto);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.MissingRequiredField, result.Error);
    }

    [Fact]
    public async Task Create_StartDateInPast_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(Create_StartDateInPast_ReturnsError));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var dto = ValidDto(startDate: FutureDate(-1));

        var result = await svc.CreateAsync(userId, dto);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.StartDateInPast, result.Error);
    }

    [Fact]
    public async Task Create_EndDateBeforeStartDate_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(Create_EndDateBeforeStartDate_ReturnsError));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var start = FutureDate(30);
        var dto = ValidDto(startDate: start, endDate: start.AddDays(-1));

        var result = await svc.CreateAsync(userId, dto);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.EndDateBeforeStartDate, result.Error);
    }

    [Fact]
    public async Task Create_DeadlineAfterStartDate_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(Create_DeadlineAfterStartDate_ReturnsError));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var start = FutureDate(30);
        // deadline == startDate should fail (must be strictly before)
        var dto = ValidDto(startDate: start, deadline: start);

        var result = await svc.CreateAsync(userId, dto);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.RegistrationDeadlineAfterStartDate, result.Error);
    }

    [Fact]
    public async Task Create_ZeroPricePerPerson_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(Create_ZeroPricePerPerson_ReturnsError));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var dto = ValidDto(pricePerPerson: 0m);

        var result = await svc.CreateAsync(userId, dto);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.InvalidPricePerPerson, result.Error);
    }

    [Fact]
    public async Task Create_NoOrganizerProfile_ReturnsOrganizerNotFound()
    {
        await using var ctx = BuildContext(nameof(Create_NoOrganizerProfile_ReturnsOrganizerNotFound));
        // Do NOT seed an organizer profile
        var svc = BuildService(ctx);

        var result = await svc.CreateAsync(999, ValidDto());

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.OrganizerNotFound, result.Error);
    }

    // ─── PublishAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_DraftTrip_MovesToGuideSelection()
    {
        await using var ctx = BuildContext(nameof(Publish_DraftTrip_MovesToGuideSelection));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(userId, ValidDto());
        Assert.True(created.Success);

        var result = await svc.PublishAsync(userId, created.Data!.Id);

        Assert.True(result.Success);
        Assert.Equal("GuideSelection", result.Data!.Status);
    }

    [Fact]
    public async Task Publish_AlreadyPublished_ReturnsInvalidTransition()
    {
        await using var ctx = BuildContext(nameof(Publish_AlreadyPublished_ReturnsInvalidTransition));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(userId, ValidDto());
        await svc.PublishAsync(userId, created.Data!.Id);  // Draft → GuideSelection

        // Second publish attempt must fail
        var result = await svc.PublishAsync(userId, created.Data.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public async Task Publish_OtherOrganizerOwnsTrip_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Publish_OtherOrganizerOwnsTrip_ReturnsUnauthorized));
        var (ownerUserId, _) = await SeedOrganizerAsync(ctx, userId: 1);
        var (otherUserId, _) = await SeedOrganizerAsync(ctx, userId: 2);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(ownerUserId, ValidDto());
        var result = await svc.PublishAsync(otherUserId, created.Data!.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task Publish_TripNotFound_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(Publish_TripNotFound_ReturnsNotFound));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.PublishAsync(userId, 9999);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.OrganizedTripNotFound, result.Error);
    }

    // ─── UpdateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_DraftTrip_UpdatesFields()
    {
        await using var ctx = BuildContext(nameof(Update_DraftTrip_UpdatesFields));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(userId, ValidDto());
        var updated = ValidDto(maxParticipants: 20, pricePerPerson: 999m);
        updated.Title = "Updated Title";

        var result = await svc.UpdateAsync(userId, created.Data!.Id, updated);

        Assert.True(result.Success);
        Assert.Equal("Updated Title", result.Data!.Title);
        Assert.Equal(20, result.Data.MaxParticipants);
        Assert.Equal(999m, result.Data.PricePerPerson);
    }

    [Fact]
    public async Task Update_GuideSelectionTrip_Succeeds()
    {
        await using var ctx = BuildContext(nameof(Update_GuideSelectionTrip_Succeeds));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(userId, ValidDto());
        await svc.PublishAsync(userId, created.Data!.Id); // → GuideSelection

        var updated = ValidDto();
        updated.Title = "Edited While In GuideSelection";

        var result = await svc.UpdateAsync(userId, created.Data.Id, updated);

        Assert.True(result.Success);
        Assert.Equal("Edited While In GuideSelection", result.Data!.Title);
    }

    [Fact]
    public async Task Update_RegistrationOpenTrip_ReturnsInvalidTransition()
    {
        await using var ctx = BuildContext(nameof(Update_RegistrationOpenTrip_ReturnsInvalidTransition));
        var (userId, profileId) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        // Directly insert a trip in RegistrationOpen status to bypass publish logic
        var trip = new OrganizedTrip
        {
            Id = 77,
            OrganizerId = profileId,
            Title = "Open Trip",
            Destination = "X",
            Description = "Y",
            StartDate = FutureDate(30),
            EndDate = FutureDate(35),
            MaxParticipants = 5,
            PricePerPerson = 100m,
            Inclusions = "Food",
            RegistrationDeadline = FutureDate(28),
            Status = OrganizedTripStatus.RegistrationOpen,
            CreatedAt = DateTime.UtcNow
        };
        ctx.OrganizedTrips.Add(trip);
        await ctx.SaveChangesAsync();

        var result = await svc.UpdateAsync(userId, trip.Id, ValidDto());

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public async Task Update_WrongOwner_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Update_WrongOwner_ReturnsUnauthorized));
        var (ownerUserId, _) = await SeedOrganizerAsync(ctx, userId: 1);
        var (otherUserId, _) = await SeedOrganizerAsync(ctx, userId: 2);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(ownerUserId, ValidDto());
        var result = await svc.UpdateAsync(otherUserId, created.Data!.Id, ValidDto());

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.Unauthorized, result.Error);
    }

    // ─── CancelAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_DraftTrip_Succeeds()
    {
        await using var ctx = BuildContext(nameof(Cancel_DraftTrip_Succeeds));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(userId, ValidDto());
        var result = await svc.CancelAsync(userId, created.Data!.Id);

        Assert.True(result.Success);
        Assert.Equal("Cancelled", result.Data!.Status);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsInvalidTransition()
    {
        await using var ctx = BuildContext(nameof(Cancel_AlreadyCancelled_ReturnsInvalidTransition));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(userId, ValidDto());
        await svc.CancelAsync(userId, created.Data!.Id);

        var result = await svc.CancelAsync(userId, created.Data.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public async Task Cancel_InProgressTrip_ReturnsInvalidTransition()
    {
        await using var ctx = BuildContext(nameof(Cancel_InProgressTrip_ReturnsInvalidTransition));
        var (userId, profileId) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var trip = new OrganizedTrip
        {
            Id = 88,
            OrganizerId = profileId,
            Title = "T",
            Destination = "D",
            Description = "X",
            StartDate = FutureDate(1),
            EndDate = FutureDate(5),
            MaxParticipants = 5,
            PricePerPerson = 100m,
            Inclusions = "F",
            RegistrationDeadline = FutureDate(-1),
            Status = OrganizedTripStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };
        ctx.OrganizedTrips.Add(trip);
        await ctx.SaveChangesAsync();

        var result = await svc.CancelAsync(userId, trip.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public async Task Cancel_WrongOwner_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Cancel_WrongOwner_ReturnsUnauthorized));
        var (ownerUserId, _) = await SeedOrganizerAsync(ctx, userId: 1);
        var (otherUserId, _) = await SeedOrganizerAsync(ctx, userId: 2);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(ownerUserId, ValidDto());
        var result = await svc.CancelAsync(otherUserId, created.Data!.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.Unauthorized, result.Error);
    }

    // ─── GetMyTripsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyTrips_WithTrips_ReturnsOrganizerTripsOnly()
    {
        await using var ctx = BuildContext(nameof(GetMyTrips_WithTrips_ReturnsOrganizerTripsOnly));
        var (userId1, _) = await SeedOrganizerAsync(ctx, userId: 1);
        var (userId2, _) = await SeedOrganizerAsync(ctx, userId: 2);
        var svc = BuildService(ctx);

        await svc.CreateAsync(userId1, ValidDto());
        await svc.CreateAsync(userId1, ValidDto());
        await svc.CreateAsync(userId2, ValidDto()); // different organizer — must not appear

        var result = await svc.GetMyTripsAsync(userId1);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Data!, t => Assert.Equal("Draft", t.Status));
    }

    [Fact]
    public async Task GetMyTrips_NoTrips_ReturnsSuccessWithEmptyList()
    {
        await using var ctx = BuildContext(nameof(GetMyTrips_NoTrips_ReturnsSuccessWithEmptyList));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.GetMyTripsAsync(userId);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetMyTrips_NoOrganizerProfile_ReturnsOrganizerNotFound()
    {
        await using var ctx = BuildContext(nameof(GetMyTrips_NoOrganizerProfile_ReturnsOrganizerNotFound));
        var svc = BuildService(ctx);

        var result = await svc.GetMyTripsAsync(999);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.OrganizerNotFound, result.Error);
    }

    // ─── GetAvailableTripsAsync (public browse) ──────────────────────────────

    private static async Task<OrganizedTrip> SeedRegistrationOpenTripAsync(
        ApplicationDbContext ctx,
        int organizerProfileId,
        int tripId,
        int maxParticipants = 5,
        int deadlineDaysFromNow = 5,
        int confirmedRegistrations = 0)
    {
        var trip = new OrganizedTrip
        {
            Id = tripId,
            OrganizerId = organizerProfileId,
            Title = $"Trip {tripId}",
            Destination = "Sylhet",
            Description = "Tea gardens",
            StartDate = FutureDate(20),
            EndDate = FutureDate(25),
            MaxParticipants = maxParticipants,
            PricePerPerson = 300m,
            Inclusions = "Food",
            RegistrationDeadline = FutureDate(deadlineDaysFromNow),
            Status = OrganizedTripStatus.RegistrationOpen,
            CreatedAt = DateTime.UtcNow
        };
        ctx.OrganizedTrips.Add(trip);

        for (int i = 0; i < confirmedRegistrations; i++)
        {
            var touristUser = new User
            {
                Id = 5000 + tripId * 100 + i,
                FullName = $"Tourist {i}",
                Email = $"t{tripId}{i}@test.com",
                PhoneNumber = "01799999990",
                Role = UserRole.Tourist,
                IsActive = true
            };
            var touristProfile = new TouristProfile
            {
                Id = 6000 + tripId * 100 + i,
                UserId = touristUser.Id
            };
            var reg = new Registration
            {
                Id = 7000 + tripId * 100 + i,
                OrganizedTripId = trip.Id,
                TouristId = touristProfile.Id,
                NumberOfTravelers = 1,
                Status = RegistrationStatus.Confirmed,
                RegisteredAt = DateTime.UtcNow
            };
            ctx.Users.Add(touristUser);
            ctx.TouristProfiles.Add(touristProfile);
            ctx.Registrations.Add(reg);
        }

        await ctx.SaveChangesAsync();
        return trip;
    }

    [Fact]
    public async Task GetAvailableTrips_ReturnsOnlyRegistrationOpenWithSpots()
    {
        await using var ctx = BuildContext(nameof(GetAvailableTrips_ReturnsOnlyRegistrationOpenWithSpots));
        var (_, profileId) = await SeedOrganizerAsync(ctx, userId: 1);
        var svc = BuildService(ctx);

        // Trip 1: RegistrationOpen, has spots
        await SeedRegistrationOpenTripAsync(ctx, profileId, tripId: 1, maxParticipants: 5);
        // Trip 2: Draft — must be excluded
        ctx.OrganizedTrips.Add(new OrganizedTrip
        {
            Id = 2, OrganizerId = profileId, Title = "Draft Trip", Destination = "X",
            Description = "Y", StartDate = FutureDate(20), EndDate = FutureDate(25),
            MaxParticipants = 5, PricePerPerson = 100m, Inclusions = "F",
            RegistrationDeadline = FutureDate(5), Status = OrganizedTripStatus.Draft,
            CreatedAt = DateTime.UtcNow
        });
        // Trip 3: RegistrationOpen but fully booked — must be excluded
        await SeedRegistrationOpenTripAsync(ctx, profileId, tripId: 3, maxParticipants: 2, confirmedRegistrations: 2);
        await ctx.SaveChangesAsync();

        var result = await svc.GetAvailableTripsAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Data![0].Id);
        Assert.True(result.Data[0].RemainingSpots > 0);
    }

    [Fact]
    public async Task GetAvailableTrips_PastDeadline_ExcludesTrip()
    {
        await using var ctx = BuildContext(nameof(GetAvailableTrips_PastDeadline_ExcludesTrip));
        var (_, profileId) = await SeedOrganizerAsync(ctx, userId: 1);
        var svc = BuildService(ctx);

        // Past deadline trip — must be excluded
        ctx.OrganizedTrips.Add(new OrganizedTrip
        {
            Id = 10,
            OrganizerId = profileId,
            Title = "Expired Trip",
            Destination = "Y",
            Description = "Z",
            StartDate = FutureDate(2),
            EndDate = FutureDate(7),
            MaxParticipants = 5,
            PricePerPerson = 100m,
            Inclusions = "F",
            RegistrationDeadline = FutureDate(-1), // yesterday
            Status = OrganizedTripStatus.RegistrationOpen,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await svc.GetAvailableTripsAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetAvailableTrips_RemainingSpots_IsCorrect()
    {
        await using var ctx = BuildContext(nameof(GetAvailableTrips_RemainingSpots_IsCorrect));
        var (_, profileId) = await SeedOrganizerAsync(ctx, userId: 1);
        var svc = BuildService(ctx);

        // 5 max, 2 confirmed → 3 remaining
        await SeedRegistrationOpenTripAsync(ctx, profileId, tripId: 1, maxParticipants: 5, confirmedRegistrations: 2);

        var result = await svc.GetAvailableTripsAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(3, result.Data![0].RemainingSpots);
    }

    [Fact]
    public async Task GetAvailableTrips_EmptyWhenNoTrips_ReturnsSuccessWithEmptyList()
    {
        await using var ctx = BuildContext(nameof(GetAvailableTrips_EmptyWhenNoTrips_ReturnsSuccessWithEmptyList));
        var svc = BuildService(ctx);

        var result = await svc.GetAvailableTripsAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
    }

    // ─── GetAvailableTripDetailAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetAvailableTripDetail_ValidId_ReturnsDetail()
    {
        await using var ctx = BuildContext(nameof(GetAvailableTripDetail_ValidId_ReturnsDetail));
        var (_, profileId) = await SeedOrganizerAsync(ctx, userId: 1);
        var svc = BuildService(ctx);

        var trip = await SeedRegistrationOpenTripAsync(ctx, profileId, tripId: 1, maxParticipants: 8);

        var result = await svc.GetAvailableTripDetailAsync(trip.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(trip.Id, result.Data!.Id);
        Assert.Equal(8, result.Data.MaxParticipants);
    }

    [Fact]
    public async Task GetAvailableTripDetail_TripNotFound_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(GetAvailableTripDetail_TripNotFound_ReturnsNotFound));
        var svc = BuildService(ctx);

        var result = await svc.GetAvailableTripDetailAsync(9999);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.OrganizedTripNotFound, result.Error);
    }

    [Fact]
    public async Task GetAvailableTripDetail_DraftTrip_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(GetAvailableTripDetail_DraftTrip_ReturnsNotFound));
        var (userId, _) = await SeedOrganizerAsync(ctx);
        var svc = BuildService(ctx);

        var created = await svc.CreateAsync(userId, ValidDto());

        // Draft trips must not be visible on the public endpoint
        var result = await svc.GetAvailableTripDetailAsync(created.Data!.Id);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.OrganizedTripNotFound, result.Error);
    }

    [Fact]
    public async Task GetAvailableTripDetail_InvalidId_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(GetAvailableTripDetail_InvalidId_ReturnsNotFound));
        var svc = BuildService(ctx);

        var result = await svc.GetAvailableTripDetailAsync(0);

        Assert.False(result.Success);
        Assert.Equal(OrganizedTripError.OrganizedTripNotFound, result.Error);
    }
}
