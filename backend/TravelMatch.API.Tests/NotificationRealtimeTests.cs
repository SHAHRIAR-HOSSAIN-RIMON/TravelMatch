using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.Hubs;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for real-time notification delivery: NotificationHub private-group isolation,
/// SignalRNotificationSender targeting, and NotificationService push-on-create behaviour.
/// </summary>
public class NotificationRealtimeTests
{
    private static ApplicationDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static User MakeUser(int id) => new()
    {
        Id = id,
        FullName = $"Test User {id}",
        Email = $"user{id}@test.com",
        PhoneNumber = $"017000000{id:D2}",
        PasswordHash = "hash",
        Role = UserRole.Tourist,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static ClaimsPrincipal CreatePrincipal(int userId) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Role, "Tourist")
    }, "TestAuth"));

    private class TrackingHubContext : TestHubCallerContext
    {
        public bool WasAborted { get; private set; }

        public TrackingHubContext(string connectionId, ClaimsPrincipal? user)
            : base(connectionId, user) { }

        public override void Abort() => WasAborted = true;
    }

    // ─── NotificationHub: private group from authenticated identity ────────

    [Fact]
    public async Task NotificationHub_OnConnected_JoinsAuthenticatedUsersPrivateGroup()
    {
        var hub = new NotificationHub(new LoggerFactory().CreateLogger<NotificationHub>());
        var clients = new TestHubCallerClients();
        var groups = new TestGroupManager();
        var context = new TrackingHubContext("conn-1", CreatePrincipal(42));

        hub.Context = context;
        hub.Clients = clients;
        hub.Groups = groups;

        await hub.OnConnectedAsync();

        Assert.False(context.WasAborted);
        Assert.Single(groups.Added);
        Assert.Equal(("conn-1", "user-42"), groups.Added[0]);
        Assert.Contains(clients.CallerProxy.Invocations,
            inv => inv.method == "JoinedNotificationsGroup" && (string)inv.args[0]! == "user-42");
    }

    [Fact]
    public async Task NotificationHub_OnConnected_WithoutUser_RejectedAndAborted()
    {
        var hub = new NotificationHub(new LoggerFactory().CreateLogger<NotificationHub>());
        var clients = new TestHubCallerClients();
        var groups = new TestGroupManager();
        var context = new TrackingHubContext("conn-2", null);

        hub.Context = context;
        hub.Clients = clients;
        hub.Groups = groups;

        await hub.OnConnectedAsync();

        Assert.True(context.WasAborted);
        Assert.Empty(groups.Added);
        Assert.Contains(clients.CallerProxy.Invocations,
            inv => inv.method == "Error" && (string)inv.args[0]! == "Unauthorized");
    }

    [Fact]
    public async Task NotificationHub_OnConnected_WithNonNumericUserId_RejectedAndAborted()
    {
        var hub = new NotificationHub(new LoggerFactory().CreateLogger<NotificationHub>());
        var clients = new TestHubCallerClients();
        var groups = new TestGroupManager();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-number")
        }, "TestAuth"));
        var context = new TrackingHubContext("conn-3", principal);

        hub.Context = context;
        hub.Clients = clients;
        hub.Groups = groups;

        await hub.OnConnectedAsync();

        Assert.True(context.WasAborted);
        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task NotificationHub_TwoUsers_JoinSeparatePrivateGroups()
    {
        var hubA = new NotificationHub(new LoggerFactory().CreateLogger<NotificationHub>());
        var groupsA = new TestGroupManager();
        hubA.Context = new TestHubCallerContext("conn-a", CreatePrincipal(1));
        hubA.Clients = new TestHubCallerClients();
        hubA.Groups = groupsA;

        var hubB = new NotificationHub(new LoggerFactory().CreateLogger<NotificationHub>());
        var groupsB = new TestGroupManager();
        hubB.Context = new TestHubCallerContext("conn-b", CreatePrincipal(2));
        hubB.Clients = new TestHubCallerClients();
        hubB.Groups = groupsB;

        await hubA.OnConnectedAsync();
        await hubB.OnConnectedAsync();

        Assert.Equal(("conn-a", "user-1"), Assert.Single(groupsA.Added));
        Assert.Equal(("conn-b", "user-2"), Assert.Single(groupsB.Added));
    }

    [Fact]
    public void NotificationHub_HasNoClientCallableGroupJoin()
    {
        // The only way into a private group is OnConnectedAsync using the
        // authenticated principal; no hub method may accept a user id from the client.
        var lifecycleOverrides = new[] { "OnConnectedAsync", "OnDisconnectedAsync" };
        var clientCallableMethods = typeof(NotificationHub)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !lifecycleOverrides.Contains(m.Name))
            .ToList();

        Assert.Empty(clientCallableMethods);
    }

    // ─── SignalRNotificationSender: delivery targeting ──────────────────────

    private static NotificationDto MakeDto(int recipientId) => new()
    {
        Id = 100 + recipientId,
        RecipientUserId = recipientId,
        Title = "Title",
        Message = "Message",
        CreatedAt = DateTime.UtcNow,
        IsRead = false
    };

    [Fact]
    public async Task SignalRNotificationSender_SendsToRecipientsPrivateGroupOnly()
    {
        var hubContext = new RecordingNotificationHubContext();
        var sender = new SignalRNotificationSender(hubContext);

        var dto = MakeDto(7);
        await sender.SendToUserAsync(dto);

        var groupProxy = Assert.Single(hubContext.GroupProxies);
        Assert.Equal("user-7", groupProxy.Key);
        var invocation = Assert.Single(groupProxy.Value.Invocations);
        Assert.Equal("ReceiveNotification", invocation.method);
        var sent = Assert.IsType<NotificationDto>(invocation.args[0]);
        Assert.Equal(7, sent.RecipientUserId);
        Assert.Equal("Title", sent.Title);
    }

    [Fact]
    public async Task SignalRNotificationSender_DifferentRecipients_GetSeparateGroups()
    {
        var hubContext = new RecordingNotificationHubContext();
        var sender = new SignalRNotificationSender(hubContext);

        await sender.SendToUserAsync(MakeDto(7));
        await sender.SendToUserAsync(MakeDto(8));

        Assert.Equal(2, hubContext.GroupProxies.Count);
        Assert.Single(hubContext.GroupProxies["user-7"].Invocations);
        Assert.Single(hubContext.GroupProxies["user-8"].Invocations);
    }

    [Fact]
    public async Task SignalRNotificationSender_GroupNameDerivedFromDto_NotCallerInput()
    {
        var hubContext = new RecordingNotificationHubContext();
        var sender = new SignalRNotificationSender(hubContext);

        // A notification persisted for user 9 must land in user-9 even though the
        // caller of CreateNotificationAsync might be any authenticated user.
        await sender.SendToUserAsync(MakeDto(9));

        Assert.Equal(new[] { "user-9" }, hubContext.GroupProxies.Keys);
    }

    // ─── NotificationService: push on create ────────────────────────────────

    [Fact]
    public async Task CreateNotificationAsync_PushesSavedNotificationToRecipientsSender()
    {
        await using var ctx = BuildContext();
        ctx.Users.Add(MakeUser(1));
        await ctx.SaveChangesAsync();

        var sender = new RecordingNotificationSender();
        var svc = new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), sender);

        var dto = await svc.CreateNotificationAsync(1, "Realtime", "You have a new notification");

        var pushed = Assert.Single(sender.Sent);
        Assert.Equal(1, pushed.RecipientUserId);
        Assert.Equal(dto.Id, pushed.Id);
        Assert.Equal("Realtime", pushed.Title);
    }

    [Fact]
    public async Task CreateNotificationAsync_PushTargetsOnlyRecipient()
    {
        await using var ctx = BuildContext();
        ctx.Users.Add(MakeUser(1));
        ctx.Users.Add(MakeUser(2));
        await ctx.SaveChangesAsync();

        var sender = new RecordingNotificationSender();
        var svc = new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), sender);

        await svc.CreateNotificationAsync(2, "For user 2", "Private message");

        var pushed = Assert.Single(sender.Sent);
        Assert.Equal(2, pushed.RecipientUserId);
        Assert.DoesNotContain(sender.Sent, n => n.RecipientUserId == 1);
    }

    [Fact]
    public async Task CreateNotificationAsync_EndToEnd_PersistsAndSendsToUserGroup()
    {
        await using var ctx = BuildContext();
        ctx.Users.Add(MakeUser(5));
        await ctx.SaveChangesAsync();

        var hubContext = new RecordingNotificationHubContext();
        var svc = new NotificationService(
            ctx,
            new LoggerFactory().CreateLogger<NotificationService>(),
            new SignalRNotificationSender(hubContext));

        var dto = await svc.CreateNotificationAsync(5, "Escrow released", "Your payment has been released.");

        // Persisted
        var dbNotif = await ctx.UserNotifications.FindAsync(dto.Id);
        Assert.NotNull(dbNotif);
        Assert.Equal(5, dbNotif!.RecipientUserId);

        // Pushed to the recipient's private group only
        var groupProxy = Assert.Single(hubContext.GroupProxies);
        Assert.Equal("user-5", groupProxy.Key);
        var invocation = Assert.Single(groupProxy.Value.Invocations);
        Assert.Equal("ReceiveNotification", invocation.method);
        Assert.Equal("Escrow released", ((NotificationDto)invocation.args[0]!).Title);
    }

    [Fact]
    public async Task CreateNotificationAsync_SenderFailure_DoesNotBreakPersistence()
    {
        await using var ctx = BuildContext();
        ctx.Users.Add(MakeUser(1));
        await ctx.SaveChangesAsync();

        var svc = new NotificationService(
            ctx,
            new LoggerFactory().CreateLogger<NotificationService>(),
            new ThrowingNotificationSender());

        var dto = await svc.CreateNotificationAsync(1, "Title", "Message");

        Assert.True(dto.Id > 0);
        var dbNotif = await ctx.UserNotifications.FindAsync(dto.Id);
        Assert.NotNull(dbNotif);
    }
}
