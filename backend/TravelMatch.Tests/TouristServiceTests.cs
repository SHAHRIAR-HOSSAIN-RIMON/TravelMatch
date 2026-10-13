
using TravelMatch.API.DTOs.Tourist;
using TravelMatch.API.Services;
using TravelMatch.Tests.TestSupport;

namespace TravelMatch.Tests;

public class TouristServiceTests
{
    [Fact]
    public async Task GetProfileAsync_ReturnsOwnProfile()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var service = new TouristService(context);

        var result = await service.GetProfileAsync(tourist.Id);

        Assert.True(result.Success);
        Assert.Equal(TouristProfileError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal(tourist.Id, result.Data!.UserId);
        Assert.Equal(tourist.Email, result.Data.Email);
        Assert.Equal("Beaches and history", result.Data.Preferences);
    }

    [Fact]
    public async Task GetProfileAsync_NeverExposesSensitiveFields()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var service = new TouristService(context);

        var result = await service.GetProfileAsync(tourist.Id);

        var exposedProperties = typeof(TouristProfileResponseDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        Assert.DoesNotContain("PasswordHash", exposedProperties);
        Assert.DoesNotContain("RefreshToken", exposedProperties);
        Assert.DoesNotContain("IsActive", exposedProperties);
    }

    [Fact]
    public async Task GetProfileAsync_ProfileMissing_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context, withProfile: false);
        var service = new TouristService(context);

        var result = await service.GetProfileAsync(tourist.Id);

        Assert.False(result.Success);
        Assert.Equal(TouristProfileError.ProfileNotFound, result.Error);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetProfileAsync_NonTourist_ReturnsUnauthorized()
    {
        await using var context = TestDatabase.CreateContext();
        var guide = Seed.AddGuide(context);
        var service = new TouristService(context);

        var result = await service.GetProfileAsync(guide.Id);

        Assert.False(result.Success);
        Assert.Equal(TouristProfileError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesExistingProfileWithoutDuplicating()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var service = new TouristService(context);

        var result = await service.UpdateProfileAsync(
            tourist.Id,
            new UpdateTouristProfileRequestDto
            {
                FullName = "  Tourist Updated  ",
                PhoneNumber = " +8801700000000 ",
                Preferences = "  Mountain trekking  "
            });

        Assert.True(result.Success);
        Assert.Equal(1, context.TouristProfiles.Count());
        Assert.Equal("Tourist Updated", result.Data!.FullName);
        Assert.Equal("+8801700000000", result.Data.PhoneNumber);
        Assert.Equal("Mountain trekking", result.Data.Preferences);

        var storedUser = context.Users.Single(user => user.Id == tourist.Id);
        var storedProfile = context.TouristProfiles.Single();

        Assert.Equal("Tourist Updated", storedUser.FullName);
        Assert.Equal("Mountain trekking", storedProfile.Preferences);
    }

    [Fact]
    public async Task UpdateProfileAsync_KeepsEmailAndRoleUnchanged()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var service = new TouristService(context);

        var result = await service.UpdateProfileAsync(
            tourist.Id,
            new UpdateTouristProfileRequestDto
            {
                FullName = "Tourist Updated"
            });

        Assert.True(result.Success);

        var storedUser = context.Users.Single(user => user.Id == tourist.Id);

        Assert.Equal(tourist.Email, storedUser.Email);
        Assert.Equal(tourist.PhoneNumber, storedUser.PhoneNumber);
    }

    [Fact]
    public async Task UpdateProfileAsync_BlankFullName_ReturnsInvalid()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var service = new TouristService(context);

        var result = await service.UpdateProfileAsync(
            tourist.Id,
            new UpdateTouristProfileRequestDto
            {
                FullName = "   "
            });

        Assert.False(result.Success);
        Assert.Equal(TouristProfileError.InvalidFullName, result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_PhoneNumberTooLong_ReturnsInvalid()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var service = new TouristService(context);

        var result = await service.UpdateProfileAsync(
            tourist.Id,
            new UpdateTouristProfileRequestDto
            {
                FullName = "Tourist One",
                PhoneNumber = new string('9', 21)
            });

        Assert.False(result.Success);
        Assert.Equal(TouristProfileError.InvalidPhoneNumber, result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_PreferencesTooLong_ReturnsInvalid()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var service = new TouristService(context);

        var result = await service.UpdateProfileAsync(
            tourist.Id,
            new UpdateTouristProfileRequestDto
            {
                FullName = "Tourist One",
                Preferences = new string('a', 1001)
            });

        Assert.False(result.Success);
        Assert.Equal(TouristProfileError.PreferencesTooLong, result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_OtherUsersProfileIsNotModified()
    {
        await using var context = TestDatabase.CreateContext();
        var touristA = Seed.AddTourist(context, "Tourist A", "a@example.com");
        var touristB = Seed.AddTourist(context, "Tourist B", "b@example.com");
        var service = new TouristService(context);

        var result = await service.UpdateProfileAsync(
            touristA.Id,
            new UpdateTouristProfileRequestDto
            {
                FullName = "Hijacked"
            });

        Assert.True(result.Success);
        Assert.Equal("Tourist B", context.Users
            .Single(user => user.Id == touristB.Id).FullName);
    }

    [Fact]
    public async Task UpdateProfileAsync_NonTourist_ReturnsUnauthorized()
    {
        await using var context = TestDatabase.CreateContext();
        var guide = Seed.AddGuide(context);
        var service = new TouristService(context);

        var result = await service.UpdateProfileAsync(
            guide.Id,
            new UpdateTouristProfileRequestDto
            {
                FullName = "Guide Attempt"
            });

        Assert.False(result.Success);
        Assert.Equal(TouristProfileError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_InactiveAccount_ReturnsInactive()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        tourist.IsActive = false;
        context.SaveChanges();

        var service = new TouristService(context);

        var result = await service.UpdateProfileAsync(
            tourist.Id,
            new UpdateTouristProfileRequestDto
            {
                FullName = "Tourist One"
            });

        Assert.False(result.Success);
        Assert.Equal(TouristProfileError.AccountInactive, result.Error);
    }
}
