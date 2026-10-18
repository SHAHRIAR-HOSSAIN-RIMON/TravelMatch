using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Messaging;
using TravelMatch.API.Hubs;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

public class SignalRHubTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<(User tourist, User guide, TripRequest tripRequest, Proposal proposal)> SeedActiveTripAsync(
        ApplicationDbContext context,
        TripRequestStatus status = TripRequestStatus.Matched)
    {
        var tourist = new User
        {
            Id = 301,
            FullName = "Tourist User",
            Email = "tourist301@test.com",
            PhoneNumber = "01733333301",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var guide = new User
        {
            Id = 302,
            FullName = "Guide User",
            Email = "guide302@test.com",
            PhoneNumber = "01733333302",
            PasswordHash = "hash",
            Role = UserRole.Guide,
            IsActive = true
        };

        var tripRequest = new TripRequest
        {
            Id = 401,
            TouristId = tourist.Id,
            Destination = "Bandarban",
            TripType = "Trekking",
            StartDate = new DateOnly(2026, 12, 10),
            EndDate = new DateOnly(2026, 12, 15),
            Budget = 25000m,
            BudgetMin = 20000m,
            BudgetMax = 30000m,
            Status = status,
            MatchedGuideId = guide.Id
        };

        var proposal = new Proposal
        {
            Id = 501,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 24000m,
            Availability = "Available",
            Status = ProposalStatus.Accepted
        };

        context.Users.AddRange(tourist, guide);
        context.TripRequests.Add(tripRequest);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        return (tourist, guide, tripRequest, proposal);
    }

    private static ClaimsPrincipal CreatePrincipal(int userId, string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth"));
    }

    [Fact]
    public async Task ItineraryTrackingHub_JoinProposalGroup_RejectsUnauthenticatedCaller()
    {
        await using var context = CreateInMemoryDbContext();
        var trackingService = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var hub = new ItineraryTrackingHub(trackingService, new LoggerFactory().CreateLogger<ItineraryTrackingHub>());
        var hubClients = new TestHubCallerClients();
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-1", null);
        hub.Clients = hubClients;
        hub.Groups = groupManager;

        await hub.JoinProposalGroup(501);

        Assert.Empty(groupManager.Added);
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "Error" && (string)inv.args[0]! == "Unauthorized");
    }

    [Fact]
    public async Task ItineraryTrackingHub_JoinProposalGroup_RejectsUnauthorizedUser()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, _, _, proposal) = await SeedActiveTripAsync(context);

        var stranger = new User
        {
            Id = 999,
            FullName = "Stranger",
            Email = "stranger@test.com",
            PhoneNumber = "01799999999",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };
        context.Users.Add(stranger);
        await context.SaveChangesAsync();

        var trackingService = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());
        var hub = new ItineraryTrackingHub(trackingService, new LoggerFactory().CreateLogger<ItineraryTrackingHub>());
        var hubClients = new TestHubCallerClients();
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-2", CreatePrincipal(stranger.Id, "Tourist"));
        hub.Clients = hubClients;
        hub.Groups = groupManager;

        await hub.JoinProposalGroup(proposal.Id);

        Assert.Empty(groupManager.Added);
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "Error" && (string)inv.args[0]! == "Not authorized to access this itinerary");
    }

    [Theory]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.InProgress)]
    public async Task ItineraryTrackingHub_JoinProposalGroup_Succeeds_ForEligibleParticipants(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, tripRequest, proposal) = await SeedActiveTripAsync(context, status);

        var trackingService = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        // Tourist test
        var touristHub = new ItineraryTrackingHub(trackingService, new LoggerFactory().CreateLogger<ItineraryTrackingHub>());
        var touristClients = new TestHubCallerClients();
        var touristGroups = new TestGroupManager();
        touristHub.Context = new TestHubCallerContext("conn-tourist", CreatePrincipal(tourist.Id, "Tourist"));
        touristHub.Clients = touristClients;
        touristHub.Groups = touristGroups;

        await touristHub.JoinProposalGroup(proposal.Id);

        Assert.Single(touristGroups.Added);
        Assert.Equal(("conn-tourist", $"proposal-{proposal.Id}"), touristGroups.Added[0]);
        Assert.Contains(touristClients.CallerProxy.Invocations, inv => inv.method == "JoinedGroup" && (string)inv.args[0]! == $"proposal-{proposal.Id}");

        // Guide test
        var guideHub = new ItineraryTrackingHub(trackingService, new LoggerFactory().CreateLogger<ItineraryTrackingHub>());
        var guideClients = new TestHubCallerClients();
        var guideGroups = new TestGroupManager();
        guideHub.Context = new TestHubCallerContext("conn-guide", CreatePrincipal(guide.Id, "Guide"));
        guideHub.Clients = guideClients;
        guideHub.Groups = guideGroups;

        await guideHub.JoinProposalGroup(proposal.Id);

        Assert.Single(guideGroups.Added);
        Assert.Equal(("conn-guide", $"proposal-{proposal.Id}"), guideGroups.Added[0]);
        Assert.Contains(guideClients.CallerProxy.Invocations, inv => inv.method == "JoinedGroup" && (string)inv.args[0]! == $"proposal-{proposal.Id}");
    }

    [Fact]
    public async Task ItineraryTrackingHub_JoinProposalGroup_RejectsCompletedTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedActiveTripAsync(context, TripRequestStatus.Completed);

        var trackingService = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());
        var hub = new ItineraryTrackingHub(trackingService, new LoggerFactory().CreateLogger<ItineraryTrackingHub>());
        var hubClients = new TestHubCallerClients();
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-3", CreatePrincipal(tourist.Id, "Tourist"));
        hub.Clients = hubClients;
        hub.Groups = groupManager;

        await hub.JoinProposalGroup(proposal.Id);

        Assert.Empty(groupManager.Added);
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "Error");
    }

    [Fact]
    public async Task ItineraryTrackingHub_LeaveProposalGroup_RemovesFromGroup()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedActiveTripAsync(context);

        var trackingService = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());
        var hub = new ItineraryTrackingHub(trackingService, new LoggerFactory().CreateLogger<ItineraryTrackingHub>());
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-leave", CreatePrincipal(tourist.Id, "Tourist"));
        hub.Groups = groupManager;

        await hub.LeaveProposalGroup(proposal.Id);

        Assert.Single(groupManager.Removed);
        Assert.Equal(("conn-leave", $"proposal-{proposal.Id}"), groupManager.Removed[0]);
    }

    [Fact]
    public async Task TripChatHub_JoinTripChat_RejectsUnauthenticatedCaller()
    {
        await using var context = CreateInMemoryDbContext();
        var messagingService = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var hub = new TripChatHub(messagingService, new LoggerFactory().CreateLogger<TripChatHub>());
        var hubClients = new TestHubCallerClients();
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-chat-1", null);
        hub.Clients = hubClients;
        hub.Groups = groupManager;

        await hub.JoinTripChat(501);

        Assert.Empty(groupManager.Added);
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "Error" && (string)inv.args[0]! == "Unauthorized");
    }

    [Fact]
    public async Task TripChatHub_JoinTripChat_RejectsUnauthorizedCaller()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, _, _, proposal) = await SeedActiveTripAsync(context);

        var stranger = new User
        {
            Id = 777,
            FullName = "Chat Stranger",
            Email = "stranger777@test.com",
            PhoneNumber = "01777777777",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };
        context.Users.Add(stranger);
        await context.SaveChangesAsync();

        var messagingService = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var hub = new TripChatHub(messagingService, new LoggerFactory().CreateLogger<TripChatHub>());
        var hubClients = new TestHubCallerClients();
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-chat-2", CreatePrincipal(stranger.Id, "Tourist"));
        hub.Clients = hubClients;
        hub.Groups = groupManager;

        await hub.JoinTripChat(proposal.Id);

        Assert.Empty(groupManager.Added);
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "Error" && (string)inv.args[0]! == "Not authorized to access this trip conversation");
    }

    [Theory]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.InProgress)]
    public async Task TripChatHub_JoinTripChat_Succeeds_ForEligibleParticipants(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedActiveTripAsync(context, status);

        var messagingService = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var hub = new TripChatHub(messagingService, new LoggerFactory().CreateLogger<TripChatHub>());
        var hubClients = new TestHubCallerClients();
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-chat-tourist", CreatePrincipal(tourist.Id, "Tourist"));
        hub.Clients = hubClients;
        hub.Groups = groupManager;

        await hub.JoinTripChat(proposal.Id);

        Assert.Single(groupManager.Added);
        Assert.Equal(("conn-chat-tourist", $"proposal-{proposal.Id}"), groupManager.Added[0]);
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "JoinedGroup" && (string)inv.args[0]! == $"proposal-{proposal.Id}");
    }

    [Fact]
    public async Task TripChatHub_JoinTripChat_RejectsCompletedTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedActiveTripAsync(context, TripRequestStatus.Completed);

        var messagingService = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var hub = new TripChatHub(messagingService, new LoggerFactory().CreateLogger<TripChatHub>());
        var hubClients = new TestHubCallerClients();
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-chat-comp", CreatePrincipal(tourist.Id, "Tourist"));
        hub.Clients = hubClients;
        hub.Groups = groupManager;

        await hub.JoinTripChat(proposal.Id);

        Assert.Empty(groupManager.Added);
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "Error");
    }

    [Fact]
    public async Task TripChatHub_SendMessage_PersistsBeforeBroadcasting_WhenAuthorized()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedActiveTripAsync(context, TripRequestStatus.InProgress);

        var messagingService = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var hub = new TripChatHub(messagingService, new LoggerFactory().CreateLogger<TripChatHub>());
        var hubClients = new TestHubCallerClients();
        var groupManager = new TestGroupManager();

        hub.Context = new TestHubCallerContext("conn-msg", CreatePrincipal(tourist.Id, "Tourist"));
        hub.Clients = hubClients;
        hub.Groups = groupManager;

        await hub.SendMessage(proposal.Id, "We reached the mountain base!");

        // 1. Verify message is persisted in PostgreSQL / DB
        var stored = await context.TripMessages.SingleOrDefaultAsync(m => m.ProposalId == proposal.Id);
        Assert.NotNull(stored);
        Assert.Equal("We reached the mountain base!", stored.Content);
        Assert.Equal(tourist.Id, stored.SenderId);

        // 2. Verify broadcast to group
        Assert.Contains(hubClients.GroupProxy.Invocations, inv =>
            inv.method == "ReceiveMessage" &&
            inv.args[0] is TripMessageDto dto &&
            dto.Content == "We reached the mountain base!");

        // 3. Verify acknowledgment sent to caller
        Assert.Contains(hubClients.CallerProxy.Invocations, inv =>
            inv.method == "MessageSent" &&
            inv.args[0] is TripMessageDto dto &&
            dto.Content == "We reached the mountain base!");
    }

    [Fact]
    public async Task TripChatHub_SendMessage_RejectsCompletedTrip_WithoutPersisting()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedActiveTripAsync(context, TripRequestStatus.Completed);

        var messagingService = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var hub = new TripChatHub(messagingService, new LoggerFactory().CreateLogger<TripChatHub>());
        var hubClients = new TestHubCallerClients();

        hub.Context = new TestHubCallerContext("conn-msg-comp", CreatePrincipal(tourist.Id, "Tourist"));
        hub.Clients = hubClients;

        await hub.SendMessage(proposal.Id, "Can't post to completed trip");

        Assert.Equal(0, await context.TripMessages.CountAsync());
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "Error");
        Assert.Empty(hubClients.GroupProxy.Invocations);
    }

    [Fact]
    public async Task TripChatHub_SendMessage_RejectsCancelledTrip_WithoutPersisting()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedActiveTripAsync(context, TripRequestStatus.Cancelled);

        var messagingService = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var hub = new TripChatHub(messagingService, new LoggerFactory().CreateLogger<TripChatHub>());
        var hubClients = new TestHubCallerClients();

        hub.Context = new TestHubCallerContext("conn-msg-canc", CreatePrincipal(tourist.Id, "Tourist"));
        hub.Clients = hubClients;

        await hub.SendMessage(proposal.Id, "Can't post to cancelled trip");

        Assert.Equal(0, await context.TripMessages.CountAsync());
        Assert.Contains(hubClients.CallerProxy.Invocations, inv => inv.method == "Error");
        Assert.Empty(hubClients.GroupProxy.Invocations);
    }
}

public class TestHubCallerContext : HubCallerContext
{
    public TestHubCallerContext(string connectionId, ClaimsPrincipal? user)
    {
        ConnectionId = connectionId;
        User = user;
    }

    public override string ConnectionId { get; }
    public override string? UserIdentifier => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public override ClaimsPrincipal? User { get; }
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features => new FeatureCollection();
    public override CancellationToken ConnectionAborted => CancellationToken.None;
    public override void Abort() { }
}

public class TestHubCallerClients : IHubCallerClients
{
    public TestClientProxy CallerProxy { get; } = new();
    public TestClientProxy GroupProxy { get; } = new();
    public TestClientProxy AllProxy { get; } = new();

    public IClientProxy Caller => CallerProxy;
    public IClientProxy Others => new TestClientProxy();
    public IClientProxy All => AllProxy;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy();
    public IClientProxy Client(string connectionId) => new TestClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new TestClientProxy();
    public IClientProxy Group(string groupName) => GroupProxy;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => GroupProxy;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => GroupProxy;
    public IClientProxy User(string userId) => new TestClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new TestClientProxy();
    public IClientProxy OthersInGroup(string groupName) => new TestClientProxy();
}

public class TestGroupManager : IGroupManager
{
    public List<(string connectionId, string groupName)> Added { get; } = new();
    public List<(string connectionId, string groupName)> Removed { get; } = new();

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Added.Add((connectionId, groupName));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Removed.Add((connectionId, groupName));
        return Task.CompletedTask;
    }
}

public class TestClientProxy : IClientProxy
{
    public List<(string method, object?[] args)> Invocations { get; } = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Invocations.Add((method, args));
        return Task.CompletedTask;
    }
}
