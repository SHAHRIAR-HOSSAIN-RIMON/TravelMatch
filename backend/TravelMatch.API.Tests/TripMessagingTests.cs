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
using TravelMatch.API.Interfaces;
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

    [Theory]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.InProgress)]
    public async Task SendMessageAsync_Succeeds_DuringPostMatchActiveStatuses(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal) = await SeedMatchedTripAsync(context);

        tripRequest.Status = status;
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.SendMessageAsync(tourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = $"Message during status {status}"
        });

        Assert.True(result.Success);
        Assert.Equal(TripMessagingError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal($"Message during status {status}", result.Data.Content);
    }

    [Theory]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.InProgress)]
    public async Task GetMessagesAsync_Succeeds_DuringPostMatchActiveStatuses(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal) = await SeedMatchedTripAsync(context);

        tripRequest.Status = status;
        context.TripMessages.Add(new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = tourist.Id,
            Content = "Existing message",
            SentAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.GetMessagesAsync(tourist.Id, proposal.Id);

        Assert.True(result.Success);
        Assert.Equal(TripMessagingError.None, result.Error);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GetMessagesAsync_Succeeds_ForCompletedTrip_ReadOnlyHistoricalAccess()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, tripRequest, proposal) = await SeedMatchedTripAsync(context);

        tripRequest.Status = TripRequestStatus.Completed;
        context.TripMessages.Add(new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = tourist.Id,
            Content = "Historical message from trip",
            SentAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        // Tourist can read historical messages
        var touristResult = await service.GetMessagesAsync(tourist.Id, proposal.Id);
        Assert.True(touristResult.Success);
        Assert.Equal(TripMessagingError.None, touristResult.Error);
        Assert.Single(touristResult.Data);

        // Guide can also read historical messages
        var guideResult = await service.GetMessagesAsync(guide.Id, proposal.Id);
        Assert.True(guideResult.Success);
        Assert.Equal(TripMessagingError.None, guideResult.Error);
        Assert.Single(guideResult.Data);
    }

    [Fact]
    public async Task SendMessageAsync_Fails_ForCompletedTrip_WriteNotAllowed()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal) = await SeedMatchedTripAsync(context);

        tripRequest.Status = TripRequestStatus.Completed;
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.SendMessageAsync(tourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Trying to post to a completed trip"
        });

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.Unauthorized, result.Error);
        Assert.Equal(0, await context.TripMessages.CountAsync());
    }

    [Fact]
    public async Task SendMessageAsync_Fails_ForCancelledTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal) = await SeedMatchedTripAsync(context);

        tripRequest.Status = TripRequestStatus.Cancelled;
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.SendMessageAsync(tourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Trying to post to a cancelled trip"
        });

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.Unauthorized, result.Error);
        Assert.Equal(0, await context.TripMessages.CountAsync());
    }

    [Fact]
    public async Task GetMessagesAsync_Fails_ForCancelledTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal) = await SeedMatchedTripAsync(context);

        tripRequest.Status = TripRequestStatus.Cancelled;
        context.TripMessages.Add(new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = tourist.Id,
            Content = "Old message",
            SentAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.GetMessagesAsync(tourist.Id, proposal.Id);

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.Unauthorized, result.Error);
    }

    [Theory]
    [InlineData(TripRequestStatus.Pending)]
    [InlineData(TripRequestStatus.ProposalsReceived)]
    public async Task Messaging_Fails_ForPreMatchStatuses(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal) = await SeedMatchedTripAsync(context);

        tripRequest.Status = status;
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var sendResult = await service.SendMessageAsync(tourist.Id, new SendMessageDto
        {
            ProposalId = proposal.Id,
            Content = "Trying to post before matching"
        });
        Assert.False(sendResult.Success);
        Assert.Equal(TripMessagingError.Unauthorized, sendResult.Error);

        var getResult = await service.GetMessagesAsync(tourist.Id, proposal.Id);
        Assert.False(getResult.Success);
        Assert.Equal(TripMessagingError.Unauthorized, getResult.Error);
    }

    // ─── Chat read receipts ─────────────────────────────────────────────────

    private static async Task<Proposal> SeedSecondProposalAsync(ApplicationDbContext context)
    {
        // Same tourist/guide pair, a second accepted proposal (another "trip").
        var tripRequest = new TripRequest
        {
            Id = 101,
            TouristId = 10,
            Destination = "Rangamati",
            TripType = "Lake",
            StartDate = new DateOnly(2027, 1, 10),
            EndDate = new DateOnly(2027, 1, 14),
            Budget = 18000m,
            BudgetMin = 15000m,
            BudgetMax = 21000m,
            Status = TripRequestStatus.Matched,
            MatchedGuideId = 20
        };

        var secondProposal = new Proposal
        {
            Id = 201,
            TripRequestId = tripRequest.Id,
            GuideId = 20,
            ProposedPrice = 17000m,
            Availability = "Available",
            Status = ProposalStatus.Accepted
        };

        context.TripRequests.Add(tripRequest);
        context.Proposals.Add(secondProposal);
        await context.SaveChangesAsync();

        return secondProposal;
    }

    private static TripMessagesController BuildReadReceiptController(
        ITripMessagingService service,
        IHubContext<TripChatHub> hubContext,
        int? authenticatedUserId)
    {
        var identity = authenticatedUserId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString()),
                new Claim(ClaimTypes.Role, "Tourist")
            }, "TestAuth");

        return new TripMessagesController(
            service,
            hubContext,
            new LoggerFactory().CreateLogger<TripMessagesController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    [Fact]
    public async Task MarkMessageAsReadAsync_ParticipantMarksOthersMessage_PersistsReadState()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedMatchedTripAsync(context);

        var message = new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = guide.Id,
            Content = "Guide message to be read",
            SentAt = DateTime.UtcNow
        };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.MarkMessageAsReadAsync(tourist.Id, proposal.Id, message.Id);

        Assert.True(result.Success);
        Assert.Equal(TripMessagingError.None, result.Error);
        Assert.True(result.WasUnreadToRead);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsRead);

        // Verify persisted state, not just the returned DTO
        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task MarkMessageAsReadAsync_SecondCall_IsIdempotentAndKeepsState()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedMatchedTripAsync(context);

        var message = new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = guide.Id,
            Content = "Read me once",
            SentAt = DateTime.UtcNow
        };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var first = await service.MarkMessageAsReadAsync(tourist.Id, proposal.Id, message.Id);
        Assert.True(first.Success);
        Assert.True(first.WasUnreadToRead);

        var second = await service.MarkMessageAsReadAsync(tourist.Id, proposal.Id, message.Id);
        Assert.True(second.Success);
        Assert.Equal(TripMessagingError.None, second.Error);
        Assert.False(second.WasUnreadToRead);
        Assert.True(second.Data!.IsRead);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task MarkMessageAsReadAsync_ScopedToProposal_DoesNotTouchMessageFromOtherTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedMatchedTripAsync(context);
        var secondProposal = await SeedSecondProposalAsync(context);

        // Message lives in proposal 200; caller asks to mark it read via proposal 201.
        var message = new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = guide.Id,
            Content = "Belongs to the first trip only",
            SentAt = DateTime.UtcNow
        };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.MarkMessageAsReadAsync(tourist.Id, secondProposal.Id, message.Id);

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.MessageNotFound, result.Error);
        Assert.Null(result.Data);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task MarkMessageAsReadAsync_NonParticipant_RejectedAndMessageUnchanged()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, _, proposal) = await SeedMatchedTripAsync(context);

        var stranger = new User
        {
            Id = 99,
            FullName = "Stranger",
            Email = "stranger@test.com",
            PhoneNumber = "01700000099",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };
        context.Users.Add(stranger);

        var message = new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = guide.Id,
            Content = "Private conversation",
            SentAt = DateTime.UtcNow
        };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.MarkMessageAsReadAsync(stranger.Id, proposal.Id, message.Id);

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.Unauthorized, result.Error);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task MarkMessageAsReadAsync_NonexistentMessage_ReturnsNotFound()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.MarkMessageAsReadAsync(tourist.Id, proposal.Id, 99999);

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.MessageNotFound, result.Error);
    }

    [Fact]
    public async Task MarkMessageAsReadAsync_OwnMessage_RejectedAndUnchanged()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        var message = new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = tourist.Id,
            Content = "My own message",
            SentAt = DateTime.UtcNow
        };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.MarkMessageAsReadAsync(tourist.Id, proposal.Id, message.Id);

        Assert.False(result.Success);
        Assert.Equal(TripMessagingError.InvalidMessage, result.Error);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task MarkMessageAsReadAsync_Succeeds_ForCompletedTrip_ReadOnlyHistoricalAccess()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, tripRequest, proposal) = await SeedMatchedTripAsync(context);

        tripRequest.Status = TripRequestStatus.Completed;
        var message = new TripMessage
        {
            ProposalId = proposal.Id,
            SenderId = guide.Id,
            Content = "Historical message",
            SentAt = DateTime.UtcNow.AddDays(-1)
        };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());

        var result = await service.MarkMessageAsReadAsync(tourist.Id, proposal.Id, message.Id);

        Assert.True(result.Success);
        Assert.True(result.WasUnreadToRead);
    }

    [Fact]
    public async Task TripMessagesController_MarkMessageAsRead_Returns401_WhenNotAuthenticated()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedMatchedTripAsync(context);

        var message = new TripMessage { ProposalId = proposal.Id, SenderId = guide.Id, Content = "Hi", SentAt = DateTime.UtcNow };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var controller = BuildReadReceiptController(service, new FakeHubContext(), authenticatedUserId: null);

        var response = await controller.MarkMessageAsRead(proposal.Id, message.Id);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task TripMessagesController_MarkMessageAsRead_Returns200AndBroadcastsOnce_ForFirstReadOnly()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedMatchedTripAsync(context);

        var message = new TripMessage { ProposalId = proposal.Id, SenderId = guide.Id, Content = "Track me", SentAt = DateTime.UtcNow };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var hubContext = new ChatRecordingHubContext();
        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var controller = BuildReadReceiptController(service, hubContext, authenticatedUserId: tourist.Id);

        var firstResponse = await controller.MarkMessageAsRead(proposal.Id, message.Id);
        var okFirst = Assert.IsType<OkObjectResult>(firstResponse);
        Assert.Equal(StatusCodes.Status200OK, okFirst.StatusCode);

        var groupProxy = Assert.Single(hubContext.GroupProxies);
        Assert.Equal($"proposal-{proposal.Id}", groupProxy.Key);
        var invocation = Assert.Single(groupProxy.Value.Invocations);
        Assert.Equal("MessageRead", invocation.method);
        Assert.Equal(message.Id, ((TripMessageDto)invocation.args[0]!).Id);

        // Repeat call: 200 success, but no second receipt event.
        var secondResponse = await controller.MarkMessageAsRead(proposal.Id, message.Id);
        Assert.IsType<OkObjectResult>(secondResponse);
        Assert.Single(hubContext.GroupProxies[$"proposal-{proposal.Id}"].Invocations);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task TripMessagesController_MarkMessageAsRead_Returns403_ForNonParticipant()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, _, proposal) = await SeedMatchedTripAsync(context);

        var message = new TripMessage { ProposalId = proposal.Id, SenderId = guide.Id, Content = "Hi", SentAt = DateTime.UtcNow };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var controller = BuildReadReceiptController(service, new FakeHubContext(), authenticatedUserId: 999);

        var response = await controller.MarkMessageAsRead(proposal.Id, message.Id);

        var objResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task TripMessagesController_MarkMessageAsRead_Returns404_ForMessageInOtherTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, _, proposal) = await SeedMatchedTripAsync(context);
        var secondProposal = await SeedSecondProposalAsync(context);

        var message = new TripMessage { ProposalId = proposal.Id, SenderId = guide.Id, Content = "Other trip", SentAt = DateTime.UtcNow };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var controller = BuildReadReceiptController(service, new FakeHubContext(), authenticatedUserId: tourist.Id);

        var response = await controller.MarkMessageAsRead(secondProposal.Id, message.Id);

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task TripMessagesController_MarkMessageAsRead_Returns400_ForOwnMessage()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal) = await SeedMatchedTripAsync(context);

        var message = new TripMessage { ProposalId = proposal.Id, SenderId = tourist.Id, Content = "Mine", SentAt = DateTime.UtcNow };
        context.TripMessages.Add(message);
        await context.SaveChangesAsync();

        var service = new TripMessagingService(context, new LoggerFactory().CreateLogger<TripMessagingService>());
        var controller = BuildReadReceiptController(service, new FakeHubContext(), authenticatedUserId: tourist.Id);

        var response = await controller.MarkMessageAsRead(proposal.Id, message.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        var stored = await context.TripMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.False(stored.IsRead);
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

public class RecordingClientProxy : IClientProxy
{
    public List<(string method, object?[] args)> Invocations { get; } = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Invocations.Add((method, args));
        return Task.CompletedTask;
    }
}

public class ChatRecordingHubContext : IHubContext<TripChatHub>
{
    public Dictionary<string, RecordingClientProxy> GroupProxies { get; } = new();

    public IHubClients Clients => new ChatRecordingHubClients(GroupProxies);

    public IGroupManager Groups => new FakeGroupManager();
}

public class ChatRecordingHubClients : IHubClients
{
    private readonly Dictionary<string, RecordingClientProxy> _groups;

    public ChatRecordingHubClients(Dictionary<string, RecordingClientProxy> groups)
    {
        _groups = groups;
    }

    private RecordingClientProxy ForGroup(string name)
    {
        if (!_groups.TryGetValue(name, out var proxy))
        {
            proxy = new RecordingClientProxy();
            _groups[name] = proxy;
        }
        return proxy;
    }

    public IClientProxy All => new RecordingClientProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new RecordingClientProxy();
    public IClientProxy Client(string connectionId) => new RecordingClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new RecordingClientProxy();
    public IClientProxy Group(string groupName) => ForGroup(groupName);
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new RecordingClientProxy();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => ForGroup(groupName);
    public IClientProxy User(string userId) => new RecordingClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new RecordingClientProxy();
}

