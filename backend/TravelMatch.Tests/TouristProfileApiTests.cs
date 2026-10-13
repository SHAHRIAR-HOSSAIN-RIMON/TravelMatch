
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TravelMatch.Tests.TestSupport;

namespace TravelMatch.Tests;

public class TouristProfileApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TouristProfileApiTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
        _client = factory.CreateClient();
    }

    private HttpClient AuthenticatedClientFor(int userId)
    {
        using var context = _factory.CreateContext();
        var user = context.Users.Single(candidate => candidate.Id == userId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.GenerateToken(user));

        return client;
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/tourist/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_Tourist_ReturnsOwnProfile()
    {
        int touristId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.GetAsync("/api/tourist/profile");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("PasswordHash", body);
        Assert.Contains(touristId.ToString(), body);
    }

    [Fact]
    public async Task GetProfile_Guide_ReturnsForbidden()
    {
        int guideId;
        using (var context = _factory.CreateContext())
        {
            guideId = Seed.AddGuide(context).Id;
        }

        using var client = AuthenticatedClientFor(guideId);

        var response = await client.GetAsync("/api/tourist/profile");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_Tourist_UpdatesOwnProfile()
    {
        int touristId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PutAsJsonAsync(
            "/api/tourist/profile",
            new
            {
                fullName = "Updated Name",
                phoneNumber = "+8801800000000",
                preferences = "Hiking"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationContext = _factory.CreateContext();
        Assert.Equal(
            "Updated Name",
            verificationContext.Users.Single(user => user.Id == touristId).FullName);
        Assert.Equal(
            "Hiking",
            verificationContext.TouristProfiles
                .Single(profile => profile.UserId == touristId)
                .Preferences);
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/tourist/profile",
            new { fullName = "No Token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_Guide_ReturnsForbidden()
    {
        int guideId;
        using (var context = _factory.CreateContext())
        {
            guideId = Seed.AddGuide(context).Id;
        }

        using var client = AuthenticatedClientFor(guideId);

        var response = await client.PutAsJsonAsync(
            "/api/tourist/profile",
            new { fullName = "Guide Attempt" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_InvalidData_ReturnsBadRequest()
    {
        int touristId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PutAsJsonAsync(
            "/api/tourist/profile",
            new
            {
                fullName = new string('a', 101),
                phoneNumber = "not a phone number",
                preferences = new string('b', 1001)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_BlankFullName_ReturnsBadRequest()
    {
        int touristId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PutAsJsonAsync(
            "/api/tourist/profile",
            new { fullName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ProfileMissing_ReturnsNotFound()
    {
        int touristId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context, withProfile: false).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PutAsJsonAsync(
            "/api/tourist/profile",
            new { fullName = "Updated Name" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_CannotTargetAnotherTourist()
    {
        int touristAId;
        int touristBId;
        using (var context = _factory.CreateContext())
        {
            touristAId = Seed.AddTourist(context, "Tourist A", "a@example.com").Id;
            touristBId = Seed.AddTourist(context, "Tourist B", "b@example.com").Id;
        }

        using var client = AuthenticatedClientFor(touristAId);

        var response = await client.PutAsJsonAsync(
            "/api/tourist/profile",
            new { fullName = "Hijacked" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationContext = _factory.CreateContext();
        Assert.Equal(
            "Tourist B",
            verificationContext.Users.Single(user => user.Id == touristBId).FullName);
    }
}
