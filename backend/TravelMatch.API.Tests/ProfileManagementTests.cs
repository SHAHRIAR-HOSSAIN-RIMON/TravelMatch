using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Profile;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for GET/PUT /api/Profile/me. HTTP tests use the real routing/JWT pipeline
/// (TestApplication) so authorization and claim-based identity are exercised.
/// </summary>
public class ProfileManagementTests
{
    private const int TouristUserId = 101;
    private const int OtherTouristUserId = 102;
    private const int GuideUserId = 201;
    private const int OrganizerUserId = 301;
    private const int AdminUserId = 401;

    // ── Infrastructure ────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ProfileController BuildController(
        ProfileService service,
        int? userId = TouristUserId,
        string? rawUserId = null)
    {
        var claims = new List<Claim>();

        if (rawUserId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, rawUserId));
        else if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));

        return new ProfileController(service)
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

    private static async Task<TestApplication> StartAsync(Action<ApplicationDbContext> seed)
    {
        var app = await TestApplication.StartAsync();
        await app.SeedAsync(db =>
        {
            seed(db);
            return Task.CompletedTask;
        });
        return app;
    }

    private static async Task<ProfileResultDto> ReadResultAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProfileResultDto>())!;

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static User NewUser(int id, string fullName, UserRole role) => new()
    {
        Id = id,
        FullName = fullName,
        Email = $"{role.ToString().ToLowerInvariant()}{id}@example.com",
        PhoneNumber = $"01700000{id:000}",
        PasswordHash = $"hash-{id}",
        Role = role,
        IsActive = true,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static void SeedTourist(
        ApplicationDbContext db,
        int userId = TouristUserId,
        string fullName = "Tourist One",
        string preferences = "Beaches")
    {
        db.Users.Add(NewUser(userId, fullName, UserRole.Tourist));
        db.TouristProfiles.Add(new TouristProfile
        {
            Id = 1000 + userId,
            UserId = userId,
            Preferences = preferences
        });
    }

    private static void SeedGuide(ApplicationDbContext db, int userId = GuideUserId)
    {
        db.Users.Add(NewUser(userId, "Guide One", UserRole.Guide));
        db.GuideProfiles.Add(new GuideProfile
        {
            Id = 2000 + userId,
            UserId = userId,
            Bio = "Original guide bio",
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure,Beach",
            ExperienceYears = 5,
            AverageRating = 4.5m,
            VerificationStatus = VerificationStatus.Verified,
            PhotoUrl = "https://example.com/guide.jpg"
        });
    }

    private static void SeedOrganizer(ApplicationDbContext db, int userId = OrganizerUserId)
    {
        db.Users.Add(NewUser(userId, "Organizer One", UserRole.Organizer));
        db.OrganizerProfiles.Add(new OrganizerProfile
        {
            Id = 3000 + userId,
            UserId = userId,
            GroupOrganizationName = "Original Group",
            Bio = "Original organizer bio"
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GET /api/Profile/me
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        await using var app = await StartAsync(db => SeedTourist(db));

        var response = await app.Client.GetAsync("/api/Profile/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Tourist_ReturnsOnlyOwnCommonAndTouristProfile()
    {
        await using var app = await StartAsync(db =>
        {
            SeedTourist(db, TouristUserId, "Tourist One", "Beaches");
            SeedTourist(db, OtherTouristUserId, "Tourist Two", "Mountains");
        });
        app.UseToken(await app.CreateTokenAsync(TouristUserId));

        var response = await app.Client.GetAsync("/api/Profile/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(TouristUserId, body.Data!.Id);
        Assert.Equal("Tourist One", body.Data.FullName);
        Assert.Equal("tourist101@example.com", body.Data.Email);
        Assert.Equal("01700000101", body.Data.PhoneNumber);
        Assert.Equal("Tourist", body.Data.Role);
        Assert.True(body.Data.IsActive);
        Assert.NotNull(body.Data.TouristProfile);
        Assert.Equal("Beaches", body.Data.TouristProfile!.Preferences);
        Assert.Null(body.Data.GuideProfile);
        Assert.Null(body.Data.OrganizerProfile);
    }

    [Fact]
    public async Task Get_Guide_ReturnsGuideProfileIncludingReadOnlyFields()
    {
        await using var app = await StartAsync(db => SeedGuide(db));
        app.UseToken(await app.CreateTokenAsync(GuideUserId));

        var response = await app.Client.GetAsync("/api/Profile/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.True(body.Success);
        Assert.Equal("Guide", body.Data!.Role);
        Assert.Null(body.Data.TouristProfile);
        Assert.Null(body.Data.OrganizerProfile);
        Assert.NotNull(body.Data.GuideProfile);
        Assert.Equal("Original guide bio", body.Data.GuideProfile!.Bio);
        Assert.Equal("Dhaka", body.Data.GuideProfile.ServiceArea);
        Assert.Equal("Adventure,Beach", body.Data.GuideProfile.SupportedTripTypes);
        Assert.Equal(5, body.Data.GuideProfile.ExperienceYears);
        Assert.Equal(4.5m, body.Data.GuideProfile.AverageRating);
        Assert.Equal("Verified", body.Data.GuideProfile.VerificationStatus);
        Assert.Equal("https://example.com/guide.jpg", body.Data.GuideProfile.PhotoUrl);
    }

    [Fact]
    public async Task Get_Organizer_ReturnsOrganizerProfile()
    {
        await using var app = await StartAsync(db => SeedOrganizer(db));
        app.UseToken(await app.CreateTokenAsync(OrganizerUserId));

        var response = await app.Client.GetAsync("/api/Profile/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.True(body.Success);
        Assert.Equal("Organizer", body.Data!.Role);
        Assert.Null(body.Data.TouristProfile);
        Assert.Null(body.Data.GuideProfile);
        Assert.NotNull(body.Data.OrganizerProfile);
        Assert.Equal("Original Group", body.Data.OrganizerProfile!.GroupOrganizationName);
        Assert.Equal("Original organizer bio", body.Data.OrganizerProfile.Bio);
    }

    [Fact]
    public async Task Get_TouristWithoutRoleProfile_ReturnsCommonProfileWithNullSection()
    {
        await using var app = await StartAsync(db =>
            db.Users.Add(NewUser(TouristUserId, "No Profile Tourist", UserRole.Tourist)));
        app.UseToken(await app.CreateTokenAsync(TouristUserId));

        var response = await app.Client.GetAsync("/api/Profile/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.True(body.Success);
        Assert.Equal(TouristUserId, body.Data!.Id);
        Assert.Null(body.Data.TouristProfile);
    }

    [Fact]
    public async Task Get_DeletedUser_Returns404()
    {
        await using var app = await StartAsync(db => SeedTourist(db));
        app.UseToken(await app.CreateTokenAsync(TouristUserId));

        await app.WithDbAsync(async db =>
        {
            db.Users.Remove(await db.Users.SingleAsync(u => u.Id == TouristUserId));
            await db.SaveChangesAsync();
            return true;
        });

        var response = await app.Client.GetAsync("/api/Profile/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.False(body.Success);
        Assert.Equal(ProfileError.UserNotFound, body.Error);
    }

    [Fact]
    public async Task Get_Admin_ReturnsCommonProfileWithoutRoleSpecificSection()
    {
        await using var app = await StartAsync(db =>
            db.Users.Add(NewUser(AdminUserId, "Admin One", UserRole.Admin)));
        app.UseToken(await app.CreateTokenAsync(AdminUserId));

        var response = await app.Client.GetAsync("/api/Profile/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.True(body.Success);
        Assert.Equal("Admin", body.Data!.Role);
        Assert.Null(body.Data.TouristProfile);
        Assert.Null(body.Data.GuideProfile);
        Assert.Null(body.Data.OrganizerProfile);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUT /api/Profile/me
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Put_Tourist_UpdatesOwnEditableFieldsAndIgnoresImmutableOnes()
    {
        await using var app = await StartAsync(db =>
        {
            SeedTourist(db, TouristUserId, "Tourist One", "Beaches");
            SeedTourist(db, OtherTouristUserId, "Tourist Two", "Mountains");
        });
        app.UseToken(await app.CreateTokenAsync(TouristUserId));

        var payload = new
        {
            id = OtherTouristUserId,
            fullName = "  Updated Tourist  ",
            phoneNumber = " 01800000101 ",
            preferences = "Updated preferences",
            email = "attacker@example.com",
            role = "Admin",
            passwordHash = "attacker-hash",
            isActive = false,
            verificationStatus = "Verified",
            averageRating = 5.0
        };

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.True(body.Success);
        Assert.Equal(TouristUserId, body.Data!.Id);
        Assert.Equal("Updated Tourist", body.Data.FullName);
        Assert.Equal("01800000101", body.Data.PhoneNumber);
        Assert.Equal("tourist101@example.com", body.Data.Email);
        Assert.Equal("Tourist", body.Data.Role);
        Assert.True(body.Data.IsActive);
        Assert.Equal("Updated preferences", body.Data.TouristProfile!.Preferences);

        var ownUser = await app.WithDbAsync(db =>
            db.Users.AsNoTracking().SingleAsync(u => u.Id == TouristUserId));
        Assert.Equal("Updated Tourist", ownUser.FullName);
        Assert.Equal("01800000101", ownUser.PhoneNumber);
        Assert.Equal("tourist101@example.com", ownUser.Email);
        Assert.Equal("hash-101", ownUser.PasswordHash);
        Assert.Equal(UserRole.Tourist, ownUser.Role);
        Assert.True(ownUser.IsActive);

        var otherUser = await app.WithDbAsync(db =>
            db.Users.AsNoTracking().SingleAsync(u => u.Id == OtherTouristUserId));
        Assert.Equal("Tourist Two", otherUser.FullName);
        Assert.Equal("01700000102", otherUser.PhoneNumber);

        var otherProfile = await app.WithDbAsync(db =>
            db.TouristProfiles.AsNoTracking().SingleAsync(p => p.UserId == OtherTouristUserId));
        Assert.Equal("Mountains", otherProfile.Preferences);
    }

    [Fact]
    public async Task Put_Guide_UpdatesEditableFieldsAndLeavesSystemFieldsUntouched()
    {
        await using var app = await StartAsync(db => SeedGuide(db));
        app.UseToken(await app.CreateTokenAsync(GuideUserId));

        var payload = new
        {
            bio = "Updated guide bio",
            serviceArea = "Chattogram",
            experienceYears = 9,
            supportedTripTypes = "Adventure,Hiking",
            photoUrl = "https://example.com/updated-guide.jpg",
            verificationStatus = "Rejected",
            averageRating = 1.0,
            email = "hacked@example.com",
            role = "Admin"
        };

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.True(body.Success);
        Assert.Equal("Guide", body.Data!.Role);
        Assert.Equal("Updated guide bio", body.Data.GuideProfile!.Bio);
        Assert.Equal("Chattogram", body.Data.GuideProfile.ServiceArea);
        Assert.Equal(9, body.Data.GuideProfile.ExperienceYears);
        Assert.Equal("Adventure,Hiking", body.Data.GuideProfile.SupportedTripTypes);
        Assert.Equal("https://example.com/updated-guide.jpg", body.Data.GuideProfile.PhotoUrl);

        var profile = await app.WithDbAsync(db =>
            db.GuideProfiles.AsNoTracking().SingleAsync(p => p.UserId == GuideUserId));
        Assert.Equal("Updated guide bio", profile.Bio);
        Assert.Equal("Chattogram", profile.ServiceArea);
        Assert.Equal(9, profile.ExperienceYears);
        Assert.Equal("Adventure,Hiking", profile.SupportedTripTypes);
        Assert.Equal("https://example.com/updated-guide.jpg", profile.PhotoUrl);
        Assert.Equal(VerificationStatus.Verified, profile.VerificationStatus);
        Assert.Equal(4.5m, profile.AverageRating);

        var getResponse = await app.Client.GetAsync("/api/Profile/me");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await ReadResultAsync(getResponse);
        Assert.Equal("Adventure,Hiking", getBody.Data!.GuideProfile!.SupportedTripTypes);
        Assert.Equal("https://example.com/updated-guide.jpg", getBody.Data.GuideProfile.PhotoUrl);

        var user = await app.WithDbAsync(db =>
            db.Users.AsNoTracking().SingleAsync(u => u.Id == GuideUserId));
        Assert.Equal("guide201@example.com", user.Email);
        Assert.Equal(UserRole.Guide, user.Role);
        Assert.Equal("hash-201", user.PasswordHash);
    }

    [Fact]
    public async Task Put_Organizer_UpdatesGroupNameAndBio()
    {
        await using var app = await StartAsync(db => SeedOrganizer(db));
        app.UseToken(await app.CreateTokenAsync(OrganizerUserId));

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", new
        {
            groupOrganizationName = "Updated Group",
            bio = "Updated organizer bio"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.True(body.Success);
        Assert.Equal("Updated Group", body.Data!.OrganizerProfile!.GroupOrganizationName);
        Assert.Equal("Updated organizer bio", body.Data.OrganizerProfile.Bio);

        var profile = await app.WithDbAsync(db =>
            db.OrganizerProfiles.AsNoTracking().SingleAsync(p => p.UserId == OrganizerUserId));
        Assert.Equal("Updated Group", profile.GroupOrganizationName);
        Assert.Equal("Updated organizer bio", profile.Bio);
    }

    [Fact]
    public async Task Put_TouristWithGuideField_Returns400AndMakesNoChanges()
    {
        await using var app = await StartAsync(db =>
            SeedTourist(db, TouristUserId, "Tourist One", "Beaches"));
        app.UseToken(await app.CreateTokenAsync(TouristUserId));

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", new
        {
            fullName = "Changed Name",
            bio = "Not allowed for tourists"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.False(body.Success);
        Assert.Equal(ProfileError.InvalidInput, body.Error);
        Assert.Null(body.Data);

        var user = await app.WithDbAsync(db =>
            db.Users.AsNoTracking().SingleAsync(u => u.Id == TouristUserId));
        Assert.Equal("Tourist One", user.FullName);

        var profile = await app.WithDbAsync(db =>
            db.TouristProfiles.AsNoTracking().SingleAsync(p => p.UserId == TouristUserId));
        Assert.Equal("Beaches", profile.Preferences);
    }

    [Fact]
    public async Task Put_TouristWithGuideOnlyFields_Returns400AndMakesNoChanges()
    {
        await using var app = await StartAsync(db =>
            SeedTourist(db, TouristUserId, "Tourist One", "Beaches"));
        app.UseToken(await app.CreateTokenAsync(TouristUserId));

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", new
        {
            supportedTripTypes = "Adventure,Hiking",
            photoUrl = "https://example.com/tourist.jpg"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.False(body.Success);
        Assert.Equal(ProfileError.InvalidInput, body.Error);
        Assert.Null(body.Data);

        var user = await app.WithDbAsync(db =>
            db.Users.AsNoTracking().SingleAsync(u => u.Id == TouristUserId));
        Assert.Equal("Tourist One", user.FullName);

        var profile = await app.WithDbAsync(db =>
            db.TouristProfiles.AsNoTracking().SingleAsync(p => p.UserId == TouristUserId));
        Assert.Equal("Beaches", profile.Preferences);
    }

    [Fact]
    public async Task Put_OrganizerWithGuideOnlyFields_Returns400AndMakesNoChanges()
    {
        await using var app = await StartAsync(db => SeedOrganizer(db));
        app.UseToken(await app.CreateTokenAsync(OrganizerUserId));

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", new
        {
            supportedTripTypes = "Adventure,Hiking",
            photoUrl = "https://example.com/organizer.jpg"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.False(body.Success);
        Assert.Equal(ProfileError.InvalidInput, body.Error);
        Assert.Null(body.Data);

        var profile = await app.WithDbAsync(db =>
            db.OrganizerProfiles.AsNoTracking().SingleAsync(p => p.UserId == OrganizerUserId));
        Assert.Equal("Original Group", profile.GroupOrganizationName);
        Assert.Equal("Original organizer bio", profile.Bio);
    }

    [Fact]
    public async Task Put_EmptyPayload_Returns400()
    {
        await using var app = await StartAsync(db => SeedTourist(db));
        app.UseToken(await app.CreateTokenAsync(TouristUserId));

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.False(body.Success);
        Assert.Equal(ProfileError.InvalidInput, body.Error);
    }

    [Fact]
    public async Task Put_NegativeExperienceYears_Returns400AndMakesNoChanges()
    {
        await using var app = await StartAsync(db => SeedGuide(db));
        app.UseToken(await app.CreateTokenAsync(GuideUserId));

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", new
        {
            experienceYears = -1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var profile = await app.WithDbAsync(db =>
            db.GuideProfiles.AsNoTracking().SingleAsync(p => p.UserId == GuideUserId));
        Assert.Equal(5, profile.ExperienceYears);
    }

    [Fact]
    public async Task Put_GuideWithoutProfile_Returns404AndDoesNotUpdateCommonFields()
    {
        await using var app = await StartAsync(db =>
            db.Users.Add(NewUser(GuideUserId, "No Profile Guide", UserRole.Guide)));
        app.UseToken(await app.CreateTokenAsync(GuideUserId));

        var response = await app.Client.PutAsJsonAsync("/api/Profile/me", new
        {
            fullName = "Should Not Persist",
            bio = "Should Not Persist"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadResultAsync(response);
        Assert.False(body.Success);
        Assert.Equal(ProfileError.ProfileNotFound, body.Error);

        var user = await app.WithDbAsync(db =>
            db.Users.AsNoTracking().SingleAsync(u => u.Id == GuideUserId));
        Assert.Equal("No Profile Guide", user.FullName);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Controller claim handling and authorization metadata
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Controller_MissingNameIdentifierClaim_Returns401ForGetAndPut()
    {
        await using var ctx = BuildContext(nameof(Controller_MissingNameIdentifierClaim_Returns401ForGetAndPut));
        SeedTourist(ctx);
        await ctx.SaveChangesAsync();

        var controller = BuildController(new ProfileService(ctx), userId: null);

        var getResult = await controller.GetMyProfile();
        Assert.IsType<UnauthorizedObjectResult>(getResult);

        var putResult = await controller.UpdateMyProfile(new UpdateProfileRequestDto
        {
            FullName = "Should Not Persist"
        });
        Assert.IsType<UnauthorizedObjectResult>(putResult);

        var user = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == TouristUserId);
        Assert.Equal("Tourist One", user.FullName);
    }

    [Fact]
    public async Task Controller_MalformedNameIdentifierClaim_Returns401()
    {
        await using var ctx = BuildContext(nameof(Controller_MalformedNameIdentifierClaim_Returns401));
        SeedTourist(ctx);
        await ctx.SaveChangesAsync();

        var controller = BuildController(
            new ProfileService(ctx),
            rawUserId: "not-an-integer");

        var result = await controller.GetMyProfile();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Controller_HasControllerLevelAuthorizeAttribute()
    {
        var attributes = typeof(ProfileController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

        Assert.NotEmpty(attributes);
    }
}
