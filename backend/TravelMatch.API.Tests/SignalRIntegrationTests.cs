using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.DTOs.Notifications;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// End-to-end SignalR delivery: real HubConnection clients connect to the hosted
/// NotificationHub over the TestServer, join their private groups, and must receive the
/// actual notification pushed when an admin resolves a dispute over the HTTP pipeline.
/// </summary>
public class SignalRIntegrationTests
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task NotificationHub_DeliversDisputeResolutionToConnectedParticipantsOnly()
    {
        await using var app = await TestApplication.StartAsync();
        var seed = await IntegrationTestData.SeedTripRequestDisputeAsync(app);

        var touristConnection = CreateConnection(app, await app.CreateTokenAsync(seed.TouristUserId));
        var guideConnection = CreateConnection(app, await app.CreateTokenAsync(seed.GuideUserId));
        var unrelatedConnection = CreateConnection(
            app, await app.CreateTokenAsync(seed.UnrelatedTouristUserId));

        try
        {
            var touristJoined = NewCompletionSource<string>();
            var guideJoined = NewCompletionSource<string>();
            var unrelatedJoined = NewCompletionSource<string>();
            var touristReceived = NewCompletionSource<NotificationDto>();
            var guideReceived = NewCompletionSource<NotificationDto>();
            var unrelatedReceived = new ConcurrentQueue<NotificationDto>();

            touristConnection.On<string>("JoinedNotificationsGroup",
                group => touristJoined.TrySetResult(group));
            guideConnection.On<string>("JoinedNotificationsGroup",
                group => guideJoined.TrySetResult(group));
            unrelatedConnection.On<string>("JoinedNotificationsGroup",
                group => unrelatedJoined.TrySetResult(group));

            touristConnection.On<NotificationDto>("ReceiveNotification",
                notification => touristReceived.TrySetResult(notification));
            guideConnection.On<NotificationDto>("ReceiveNotification",
                notification => guideReceived.TrySetResult(notification));
            unrelatedConnection.On<NotificationDto>("ReceiveNotification",
                notification => unrelatedReceived.Enqueue(notification));

            await touristConnection.StartAsync();
            await guideConnection.StartAsync();
            await unrelatedConnection.StartAsync();

            Assert.Equal(
                $"user-{seed.TouristUserId}",
                await touristJoined.Task.WaitAsync(DeliveryTimeout));
            Assert.Equal(
                $"user-{seed.GuideUserId}",
                await guideJoined.Task.WaitAsync(DeliveryTimeout));
            Assert.Equal(
                $"user-{seed.UnrelatedTouristUserId}",
                await unrelatedJoined.Task.WaitAsync(DeliveryTimeout));

            app.UseToken(await app.CreateTokenAsync(seed.AdminUserId));
            var resolveResponse = await app.Client.PostAsJsonAsync(
                $"/api/disputes/{seed.DisputeId}/resolve",
                new ResolveDisputeDto { ResolutionNote = "Split refund approved" });

            Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

            var touristNotification = await touristReceived.Task.WaitAsync(DeliveryTimeout);
            Assert.Equal("Dispute resolved", touristNotification.Title);
            Assert.Equal(seed.TouristUserId, touristNotification.RecipientUserId);
            Assert.Contains("Split refund approved", touristNotification.Message);

            var guideNotification = await guideReceived.Task.WaitAsync(DeliveryTimeout);
            Assert.Equal("Dispute resolved", guideNotification.Title);
            Assert.Equal(seed.GuideUserId, guideNotification.RecipientUserId);

            await Task.Delay(300);
            Assert.Empty(unrelatedReceived);
        }
        finally
        {
            await DisposeConnectionAsync(touristConnection);
            await DisposeConnectionAsync(guideConnection);
            await DisposeConnectionAsync(unrelatedConnection);
        }
    }

    private static HubConnection CreateConnection(TestApplication app, string token) =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/notifications", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => app.CreateServerHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private static TaskCompletionSource<T> NewCompletionSource<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task DisposeConnectionAsync(HubConnection connection)
    {
        try
        {
            await connection.StopAsync();
        }
        catch
        {
            // Cleanup only; a failed stop must not mask the test result.
        }

        await connection.DisposeAsync();
    }
}
