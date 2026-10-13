
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using TravelMatch.Tests.TestSupport;

namespace TravelMatch.Tests;

public class TripRequestCancelServiceTests
{
    [Fact]
    public async Task CancelAsync_OwnRequest_SetsStatusToCancelled()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var tripRequest = Seed.AddTripRequest(context, tourist.Id);
        var service = new TripRequestService(context);

        var result = await service.CancelAsync(tourist.Id, tripRequest.Id);

        Assert.True(result.Success);
        Assert.Equal(TripRequestError.None, result.Error);
        Assert.Equal("Cancelled", result.Data!.Status);
        Assert.NotNull(result.Data.UpdatedAt);
        Assert.Equal(
            TripRequestStatus.Cancelled,
            context.TripRequests.Single().Status);
    }

    [Fact]
    public async Task CancelAsync_KeepsRowForHistory()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var tripRequest = Seed.AddTripRequest(context, tourist.Id);
        var service = new TripRequestService(context);

        await service.CancelAsync(tourist.Id, tripRequest.Id);

        Assert.Equal(1, context.TripRequests.Count());
    }

    [Fact]
    public async Task CancelAsync_AnotherTouristsRequest_IsDenied()
    {
        await using var context = TestDatabase.CreateContext();
        var touristA = Seed.AddTourist(context, "Tourist A", "a@example.com");
        var touristB = Seed.AddTourist(context, "Tourist B", "b@example.com");
        var tripRequest = Seed.AddTripRequest(context, touristB.Id);
        var service = new TripRequestService(context);

        var result = await service.CancelAsync(touristA.Id, tripRequest.Id);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.NotTripRequestOwner, result.Error);
        Assert.Equal(
            TripRequestStatus.Open,
            context.TripRequests.Single().Status);
    }

    [Fact]
    public async Task CancelAsync_UnknownRequest_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var service = new TripRequestService(context);

        var result = await service.CancelAsync(tourist.Id, 9999);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.TripRequestNotFound, result.Error);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var tripRequest = Seed.AddTripRequest(
            context,
            tourist.Id,
            TripRequestStatus.Cancelled);
        var service = new TripRequestService(context);

        var result = await service.CancelAsync(tourist.Id, tripRequest.Id);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.AlreadyCancelled, result.Error);
    }

    [Fact]
    public async Task CancelAsync_CompletedRequest_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var tourist = Seed.AddTourist(context);
        var tripRequest = Seed.AddTripRequest(
            context,
            tourist.Id,
            TripRequestStatus.Completed);
        var service = new TripRequestService(context);

        var result = await service.CancelAsync(tourist.Id, tripRequest.Id);

        Assert.False(result.Success);
        Assert.Equal(TripRequestError.TripRequestCompleted, result.Error);
        Assert.Equal(
            TripRequestStatus.Completed,
            context.TripRequests.Single().Status);
    }
}
