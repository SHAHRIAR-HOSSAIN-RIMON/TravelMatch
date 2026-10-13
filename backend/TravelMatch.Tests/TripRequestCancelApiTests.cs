
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Models;
using TravelMatch.Tests.TestSupport;

namespace TravelMatch.Tests;

public class TripRequestCancelApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TripRequestCancelApiTests(ApiWebApplicationFactory factory)
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
    public async Task Cancel_OwnRequest_ReturnsCancelledStatus()
    {
        int touristId;
        int tripRequestId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
            tripRequestId = Seed.AddTripRequest(context, touristId).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PatchAsync(
            $"/api/trip-requests/{tripRequestId}/cancel",
            content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cancelled", body);

        using var verificationContext = _factory.CreateContext();
        Assert.Equal(
            TripRequestStatus.Cancelled,
            verificationContext.TripRequests
                .Single(request => request.Id == tripRequestId)
                .Status);
    }

    [Fact]
    public async Task Cancel_Unauthenticated_ReturnsUnauthorized()
    {
        int touristId;
        int tripRequestId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
            tripRequestId = Seed.AddTripRequest(context, touristId).Id;
        }

        var response = await _client.PatchAsync(
            $"/api/trip-requests/{tripRequestId}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_NonTourist_ReturnsForbidden()
    {
        int guideId;
        int tripRequestId;
        using (var context = _factory.CreateContext())
        {
            guideId = Seed.AddGuide(context).Id;
            var tourist = Seed.AddTourist(context, "Tourist X", "x@example.com");
            tripRequestId = Seed.AddTripRequest(context, tourist.Id).Id;
        }

        using var client = AuthenticatedClientFor(guideId);

        var response = await client.PatchAsync(
            $"/api/trip-requests/{tripRequestId}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_AnotherTouristsRequest_ReturnsForbidden()
    {
        int touristAId;
        int tripRequestId;
        using (var context = _factory.CreateContext())
        {
            touristAId = Seed.AddTourist(context, "Tourist A", "a@example.com").Id;
            var touristB = Seed.AddTourist(context, "Tourist B", "b@example.com");
            tripRequestId = Seed.AddTripRequest(context, touristB.Id).Id;
        }

        using var client = AuthenticatedClientFor(touristAId);

        var response = await client.PatchAsync(
            $"/api/trip-requests/{tripRequestId}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verificationContext = _factory.CreateContext();
        Assert.Equal(
            TripRequestStatus.Pending,
            verificationContext.TripRequests
                .Single(request => request.Id == tripRequestId)
                .Status);
    }

    [Fact]
    public async Task Cancel_UnknownRequest_ReturnsNotFound()
    {
        int touristId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PatchAsync(
            "/api/trip-requests/9999/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsConflict()
    {
        int touristId;
        int tripRequestId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
            tripRequestId = Seed.AddTripRequest(
                context,
                touristId,
                TripRequestStatus.Cancelled).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PatchAsync(
            $"/api/trip-requests/{tripRequestId}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_CompletedRequest_ReturnsConflict()
    {
        int touristId;
        int tripRequestId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
            tripRequestId = Seed.AddTripRequest(
                context,
                touristId,
                TripRequestStatus.Completed).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PatchAsync(
            $"/api/trip-requests/{tripRequestId}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ExistingControllerRouteStillResolves()
    {
        int touristId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PostAsJsonAsync(
            "/api/TripRequests",
            new CreateTripRequestDto
            {
                Destination = "Sundarbans",
                TripType = "Nature",
                StartDate = new DateOnly(2027, 3, 1),
                EndDate = new DateOnly(2027, 3, 4),
                NumberOfTravelers = 2,
                Budget = 30000m,
                Description = "Boat safari"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_KebabCaseAliasRouteResolves()
    {
        int touristId;
        using (var context = _factory.CreateContext())
        {
            touristId = Seed.AddTourist(context).Id;
        }

        using var client = AuthenticatedClientFor(touristId);

        var response = await client.PostAsJsonAsync(
            "/api/trip-requests",
            new CreateTripRequestDto
            {
                Destination = "Sundarbans",
                TripType = "Nature",
                StartDate = new DateOnly(2027, 3, 1),
                EndDate = new DateOnly(2027, 3, 4),
                NumberOfTravelers = 2,
                Budget = 30000m,
                Description = "Boat safari"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
