using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for TripRequestService: create, list, read, update, and lifecycle transitions.
/// Uses EF Core InMemory provider so no real database is needed.
/// </summary>
public class TripRequestServiceTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static User MakeTourist(int id) => new()
    {
        Id = id,
        FullName = "Test Tourist",
        Email = $"tourist{id}@test.com",
        PhoneNumber = "01700000000",
        PasswordHash = "hash",
        Role = UserRole.Tourist,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static DateOnly FutureDate(int daysFromNow = 30) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow));

    private static CreateTripRequestDto ValidCreateDto() => new()
    {
        Destination = "Cox's Bazar",
        TripType = "Beach",
        StartDate = FutureDate(30),
        EndDate = FutureDate(35),
        NumberOfTravelers = 2,
        Budget = 20000m,
        BudgetMin = 15000m,
        BudgetMax = 25000m,
        Description = "A relaxing beach trip.",
        TravelPreferences = "Vegetarian meals"
    };

    // ─── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SuccessCase_ReturnsTripRequest()
    {
        await using var ctx = BuildContext(nameof(CreateAsync_SuccessCase_ReturnsTripRequest));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var result = await svc.CreateAsync(1, ValidCreateDto());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Cox's Bazar", result.Data!.Destination);
        Assert.Equal("Pending", result.Data.Status);
        Assert.Equal(1, result.Data.TouristId);
    }

    [Fact]
    public async Task CreateAsync_StartDateInPast_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(CreateAsync_StartDateInPast_ReturnsError));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var dto = ValidCreateDto();
        dto.StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

        var result = await svc.CreateAsync(1, dto);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.StartDateInPast, result.Error);
    }

    [Fact]
    public async Task CreateAsync_EndDateBeforeStartDate_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(CreateAsync_EndDateBeforeStartDate_ReturnsError));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var dto = ValidCreateDto();
        dto.StartDate = FutureDate(30);
        dto.EndDate = FutureDate(20); // before start

        var result = await svc.CreateAsync(1, dto);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.EndDateBeforeStartDate, result.Error);
    }

    [Fact]
    public async Task CreateAsync_ZeroBudget_ReturnsInvalidBudgetError()
    {
        await using var ctx = BuildContext(nameof(CreateAsync_ZeroBudget_ReturnsInvalidBudgetError));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var dto = ValidCreateDto();
        dto.Budget = 0m;

        var result = await svc.CreateAsync(1, dto);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.InvalidBudget, result.Error);
    }

    [Fact]
    public async Task CreateAsync_BudgetMinExceedsMax_ReturnsInvalidBudgetError()
    {
        await using var ctx = BuildContext(nameof(CreateAsync_BudgetMinExceedsMax_ReturnsInvalidBudgetError));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var dto = ValidCreateDto();
        dto.BudgetMin = 30000m;
        dto.BudgetMax = 10000m;

        var result = await svc.CreateAsync(1, dto);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.InvalidBudget, result.Error);
    }

    [Fact]
    public async Task CreateAsync_NonExistentTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(CreateAsync_NonExistentTourist_ReturnsUnauthorized));
        // No user added

        var svc = new TripRequestService(ctx);
        var result = await svc.CreateAsync(99, ValidCreateDto());

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task CreateAsync_InactiveTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(CreateAsync_InactiveTourist_ReturnsUnauthorized));
        var tourist = MakeTourist(1);
        tourist.IsActive = false;
        ctx.Users.Add(tourist);
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var result = await svc.CreateAsync(1, ValidCreateDto());

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task CreateAsync_ZeroTravelers_ReturnsInvalidTravelerError()
    {
        await using var ctx = BuildContext(nameof(CreateAsync_ZeroTravelers_ReturnsInvalidTravelerError));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var dto = ValidCreateDto();
        dto.NumberOfTravelers = 0;

        var result = await svc.CreateAsync(1, dto);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.InvalidNumberOfTravelers, result.Error);
    }

    // ─── GetMyRequestsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetMyRequestsAsync_ReturnsOnlyOwnRequests()
    {
        await using var ctx = BuildContext(nameof(GetMyRequestsAsync_ReturnsOnlyOwnRequests));
        ctx.Users.Add(MakeTourist(1));
        ctx.Users.Add(MakeTourist(2));
        await ctx.SaveChangesAsync();

        ctx.TripRequests.Add(new TripRequest
        {
            TouristId = 1, Destination = "Sylhet", TripType = "Nature",
            StartDate = FutureDate(30), EndDate = FutureDate(35),
            NumberOfTravelers = 2, Budget = 10000m, BudgetMin = 8000m, BudgetMax = 12000m,
            Status = TripRequestStatus.Pending, CreatedAt = DateTime.UtcNow
        });
        ctx.TripRequests.Add(new TripRequest
        {
            TouristId = 2, Destination = "Dhaka", TripType = "City",
            StartDate = FutureDate(30), EndDate = FutureDate(35),
            NumberOfTravelers = 1, Budget = 5000m, BudgetMin = 4000m, BudgetMax = 6000m,
            Status = TripRequestStatus.Pending, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var result = await svc.GetMyRequestsAsync(1);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Sylhet", result.Data[0].Destination);
    }

    [Fact]
    public async Task GetMyRequestsAsync_NoRequests_ReturnsEmptyList()
    {
        await using var ctx = BuildContext(nameof(GetMyRequestsAsync_NoRequests_ReturnsEmptyList));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var result = await svc.GetMyRequestsAsync(1);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Data);
    }

    // ─── GetMyRequestAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMyRequestAsync_OwnerCanRead()
    {
        await using var ctx = BuildContext(nameof(GetMyRequestAsync_OwnerCanRead));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var created = await svc.CreateAsync(1, ValidCreateDto());
        var tripId = created.Data!.Id;

        var result = await svc.GetMyRequestAsync(1, tripId);

        Assert.True(result.Success);
        Assert.Equal(tripId, result.Data!.Id);
    }

    [Fact]
    public async Task GetMyRequestAsync_OtherTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(GetMyRequestAsync_OtherTourist_ReturnsUnauthorized));
        ctx.Users.Add(MakeTourist(1));
        ctx.Users.Add(MakeTourist(2));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var created = await svc.CreateAsync(1, ValidCreateDto());
        var tripId = created.Data!.Id;

        var result = await svc.GetMyRequestAsync(2, tripId);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task GetMyRequestAsync_NonExistentRequest_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(GetMyRequestAsync_NonExistentRequest_ReturnsNotFound));
        var svc = new TripRequestService(ctx);

        var result = await svc.GetMyRequestAsync(1, 9999);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.TouristNotFound, result.Error);
    }

    // ─── UpdateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_PendingRequest_UpdatesFields()
    {
        await using var ctx = BuildContext(nameof(UpdateAsync_PendingRequest_UpdatesFields));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var created = await svc.CreateAsync(1, ValidCreateDto());
        var tripId = created.Data!.Id;

        var updateDto = new UpdateTripRequestDto
        {
            Destination = "Bandarban",
            NumberOfTravelers = 4
        };

        var result = await svc.UpdateAsync(1, tripId, updateDto);

        Assert.True(result.Success);
        Assert.Equal("Bandarban", result.Data!.Destination);
        Assert.Equal(4, result.Data.NumberOfTravelers);
        // Unchanged fields must stay
        Assert.Equal("Beach", result.Data.TripType);
    }

    [Fact]
    public async Task UpdateAsync_NonPendingRequest_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(UpdateAsync_NonPendingRequest_ReturnsError));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var tripRequest = new TripRequest
        {
            TouristId = 1, Destination = "Dhaka", TripType = "City",
            StartDate = FutureDate(30), EndDate = FutureDate(35),
            NumberOfTravelers = 1, Budget = 5000m, BudgetMin = 4000m, BudgetMax = 6000m,
            Status = TripRequestStatus.ProposalsReceived, CreatedAt = DateTime.UtcNow
        };
        ctx.TripRequests.Add(tripRequest);
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var result = await svc.UpdateAsync(1, tripRequest.Id, new UpdateTripRequestDto
        {
            Destination = "Chittagong"
        });

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_OtherTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(UpdateAsync_OtherTourist_ReturnsUnauthorized));
        ctx.Users.Add(MakeTourist(1));
        ctx.Users.Add(MakeTourist(2));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var created = await svc.CreateAsync(1, ValidCreateDto());
        var tripId = created.Data!.Id;

        var result = await svc.UpdateAsync(2, tripId, new UpdateTripRequestDto
        {
            Destination = "Intruder Destination"
        });

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_InvalidBudgetRange_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(UpdateAsync_InvalidBudgetRange_ReturnsError));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var created = await svc.CreateAsync(1, ValidCreateDto());
        var tripId = created.Data!.Id;

        var result = await svc.UpdateAsync(1, tripId, new UpdateTripRequestDto
        {
            BudgetMin = 50000m,
            BudgetMax = 10000m
        });

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.InvalidBudget, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_StartDateInPast_ReturnsError()
    {
        await using var ctx = BuildContext(nameof(UpdateAsync_StartDateInPast_ReturnsError));
        ctx.Users.Add(MakeTourist(1));
        await ctx.SaveChangesAsync();

        var svc = new TripRequestService(ctx);
        var created = await svc.CreateAsync(1, ValidCreateDto());
        var tripId = created.Data!.Id;

        var result = await svc.UpdateAsync(1, tripId, new UpdateTripRequestDto
        {
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
        });

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.StartDateInPast, result.Error);
    }
}
