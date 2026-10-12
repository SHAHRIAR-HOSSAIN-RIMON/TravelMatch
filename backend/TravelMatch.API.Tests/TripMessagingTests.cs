using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Messaging;
using TravelMatch.API.Hubs;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

public class TripMessagingTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<(User tourist, User guide, TripRequest request, Proposal proposal)> SeedMatchedTripAsync(
        ApplicationDbContext context)
    {
        var tourist = new User
        {
            Id = 10,
            FullName = "Jane Tourist",
            Email = "tourist@test.com",
            PhoneNumber = "01700000001",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var guide = new User
        {
            Id = 20,
            FullName = "John Guide",
            Email = "guide@test.com",
            PhoneNumber = "01700000002",
            PasswordHash = "hash",
            Role = UserRole.Guide,
            IsActive = true
        };

        var tripRequest = new TripRequest
        {
            Id = 100,
            TouristId = tourist.Id,
            Destination = "Sylhet",
            TripType = "Nature",
            StartDate = new DateOnly(2026, 12, 1),
            EndDate = new DateOnly(2026, 12, 5),
            Budget = 15000m,
            BudgetMin = 10000m,
            BudgetMax = 20000m,
            Status = TripRequestStatus.Matched,
            MatchedGuideId = guide.Id
        };

        var proposal = new Proposal
        {
            Id = 200,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 14000m,
            Availability = "Available",
            Status = ProposalStatus.Accepted
        };

        context.Users.AddRange(tourist, guide);
        context.TripRequests.Add(tripRequest);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        return (tourist, guide, tripRequest, proposal);
    }

    [Fact]
    public async Task SendMessageAsync_PersistsMessageAndReturnsSuccess_WhenAuthorized()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedMatchedTripAsync(context);

        var logger = new LoggerFactory().CreateLogger<TripMessagingService>();
        var service = new TripMessagingService(context, logger);

        var sendDto = new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Hello, looking forward to the trip!"
        };

        var result = await service.SendMessageAsync(tourist.Id, sendDto);

        Assert.True(result.Success);
        Assert.Equal(TripMessagingError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal("Hello, looking forward to the trip!", result.Data.Content);
        Assert.Equal(tourist.Id, result.Data.SenderId);
        Assert.Equal(tourist.FullName, result.Data.SenderName);
        Assert.Equal("Tourist", result.Data.SenderRole);

        // Verify database persistence
        var stored = await context.TripMessages.FirstOrDefaultAsync(m => m.ProposalId == proposal.Id);
        Assert.NotNull(stored);
        Assert.Equal("Hello, looking forward to the trip!", stored.Content);
        Assert.Equal(tourist.Id, stored.SenderId);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task SendMessageAsync_GuideCanAlsoSendMessage_WhenAuthorized()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, _, proposal) = await SeedMatchedTripAsync(context);

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.SendMessageAsync(guide.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Welcome! I have prepared the route."
        });

        Assert.True(result.Success);
        Assert.Equal(guide.Id, result.Data!.SenderId);
        Assert.Equal("Guide", result.Data.SenderRole);

        var count = await context.TripMessages.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SendMessageAsync_RejectsUnauthorizedUser()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, _, _, proposal) = await SeedMatchedTripAsync(context);

        var randomTourist = new User
        {
            Id = 99,
            FullName = "Stranger",
            Email = "stranger@test.com",
            PhoneNumber = "01700000099",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };
        context.Users.Add(randomTourist);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.SendMessageAsync(randomTourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Trying to intrude"
        });

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.Unauthorized, result.Error);

        var count = await context.TripMessages.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SendMessageAsync_RejectsInactiveUser()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        tourist.IsActive = false;
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.SendMessageAsync(tourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Hello?"
        });

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task SendMessageAsync_RejectsUnacceptedProposal()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        proposal.Status = ProposalStatus.Submitted;
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.SendMessageAsync(tourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Proposal is only submitted, not accepted yet"
        });

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task SendMessageAsync_RejectsEmptyOrWhitespaceMessage()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.SendMessageAsync(tourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "   "
        });

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.InvalidMessage, result.Error);
    }

    [Fact]
    public async Task SendMessageAsync_RejectsMessageExceedingMaxLength()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var longContent = new string('a', 2001);
        var result = await service.SendMessageAsync(tourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = longContent
        });

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.InvalidMessage, result.Error);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsOrderedHistoryWithPagination()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedMatchedTripAsync(context);

        context.TripMessages.AddRange(
            new TripMessage { Id = 1, ProposalId = proposal.Id, SenderId = tourist.Id, Content = "Msg 1", SentAt = DateTime.UtcNow.AddMinutes(-10) },
            new TripMessage { Id = 2, ProposalId = proposal.Id, SenderId = guide.Id, Content = "Msg 2", SentAt = DateTime.UtcNow.AddMinutes(-8) },
            new TripMessage { Id = 3, ProposalId = proposal.Id, SenderId = tourist.Id, Content = "Msg 3", SentAt = DateTime.UtcNow.AddMinutes(-5) }
        );
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.GetMessagesAsync(tourist.Id, proposal.Id, page: 1, pageSize: 2);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("Msg 1", result.Data[0].Content);
        Assert.Equal("Msg 2", result.Data[1].Content);
        Assert.True(result.HasMore);

        var page2 = await service.GetMessagesAsync(tourist.Id, proposal.Id, page: 2, pageSize: 2);
        Assert.Single(page2.Data);
        Assert.Equal("Msg 3", page2.Data[0].Content);
        Assert.False(page2.HasMore);
    }

    [Fact]
    public async Task GetMessagesAsync_FiltersWithBeforeId_ForOfflineOrEarlierCatchUp()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        context.TripMessages.AddRange(
            new TripMessage { Id = 1, ProposalId = proposal.Id, SenderId = tourist.Id, Content = "Old 1", SentAt = DateTime.UtcNow.AddMinutes(-20) },
            new TripMessage { Id = 2, ProposalId = proposal.Id, SenderId = tourist.Id, Content = "Old 2", SentAt = DateTime.UtcNow.AddMinutes(-15) },
            new TripMessage { Id = 3, ProposalId = proposal.Id, SenderId = tourist.Id, Content = "New 3", SentAt = DateTime.UtcNow.AddMinutes(-5) }
        );
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.GetMessagesAsync(tourist.Id, proposal.Id, page: 1, pageSize: 10, beforeId: 3);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(new[] { 1, 2 }, result.Data.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task TripMessagesController_Returns401_WhenUserNotAuthenticated()
    {
        await using var context = CreateInMemoryDbContext();
        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var controller = new TripMessagesController(service, new FakeHubContext(), new LoggerFactory().CreateLogger<TripMessagesController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var response = await controller.GetMessages(100);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task TripMessagesController_Returns403_WhenUserNotParticipant()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, _, _, proposal) = await SeedMatchedTripAsync(context);

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var controller = new TripMessagesController(service, new FakeHubContext(), new LoggerFactory().CreateLogger<TripMessagesController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "999"),
                        new Claim(ClaimTypes.Role, "Tourist")
                    }, "TestAuth"))
                }
            }
        };

        var response = await controller.GetMessages(proposal.Id);
        var objResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
    }

    [Fact]
    public async Task TripMessagesController_Returns201Created_OnValidMessage()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var controller = new TripMessagesController(service, new FakeHubContext(), new LoggerFactory().CreateLogger<TripMessagesController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, tourist.Id.ToString()),
                        new Claim(ClaimTypes.Role, "Tourist")
                    }, "TestAuth"))
                }
            }
        };

        var response = await controller.SendMessage(proposal.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Valid chat message"
        });

        var createdResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);

        var stored = await context.TripMessages.SingleOrDefaultAsync();
        Assert.NotNull(stored);
        Assert.Equal("Valid chat message", stored.Content);
    }
}

public class FakeHubContext : IHubContext<TripChatHub>
{
    public IHubClients Clients { get; } = new FakeHubClients();
    public IGroupManager Groups { get; } = new FakeGroupManager();
}

public class FakeHubClients : IHubClients
{
    public IClientProxy All => new FakeClientProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
    public IClientProxy Client(string connectionId) => new FakeClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
    public IClientProxy Group(string groupName) => new FakeClientProxy();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
    public IClientProxy User(string userId) => new FakeClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
}

public class FakeGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeClientProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

