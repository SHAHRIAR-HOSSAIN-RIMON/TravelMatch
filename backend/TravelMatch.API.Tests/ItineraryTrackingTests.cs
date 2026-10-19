using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.ItineraryTracking;
using TravelMatch.API.Hubs;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

public class ItineraryTrackingTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<(User tourist, User guide, TripRequest request, Proposal proposal, ItineraryDay day, ItineraryActivity activity)> SeedTripWithItineraryAsync(
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

        var day = new ItineraryDay
        {
            Id = 300,
            ProposalId = proposal.Id,
            DayNumber = 1,
            Title = "Day 1 Arrival",
            Description = "Arrival and setup",
            Activities = "Sightseeing",
            Schedule = "Morning",
            CreatedAt = DateTime.UtcNow
        };

        var activity = new ItineraryActivity
        {
            Id = 400,
            ItineraryDayId = day.Id,
            OrderIndex = 1,
            Title = "Visit tea garden",
            Description = "Walk through the tea garden",
            Status = ActivityStatus.Upcoming,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(tourist, guide);
        context.TripRequests.Add(tripRequest);
        context.Proposals.Add(proposal);
        context.ItineraryDays.Add(day);
        context.ItineraryActivities.Add(activity);
        await context.SaveChangesAsync();

        return (tourist, guide, tripRequest, proposal, day, activity);
    }

    [Theory]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.InProgress)]
    public async Task GetItineraryTrackingAsync_Succeeds_DuringPostMatchActiveStatuses(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal, _, _) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = status;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.GetItineraryTrackingAsync(tourist.Id, proposal.Id);

        Assert.True(result.Success);
        Assert.Equal(ItineraryTrackingError.None, result.Error);
        Assert.Single(result.Data);
        Assert.Single(result.Data[0].ActivitiesList);
    }

    [Fact]
    public async Task GetItineraryTrackingAsync_Succeeds_ForCompletedTrip_ReadOnlyHistoricalAccess()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, tripRequest, proposal, _, _) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = TripRequestStatus.Completed;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        // Tourist historical view
        var touristResult = await service.GetItineraryTrackingAsync(tourist.Id, proposal.Id);
        Assert.True(touristResult.Success);
        Assert.Equal(ItineraryTrackingError.None, touristResult.Error);
        Assert.Single(touristResult.Data);

        // Guide historical view
        var guideResult = await service.GetItineraryTrackingAsync(guide.Id, proposal.Id);
        Assert.True(guideResult.Success);
        Assert.Equal(ItineraryTrackingError.None, guideResult.Error);
        Assert.Single(guideResult.Data);
    }

    [Fact]
    public async Task GetItineraryTrackingAsync_Fails_ForCancelledTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal, _, _) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = TripRequestStatus.Cancelled;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.GetItineraryTrackingAsync(tourist.Id, proposal.Id);

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
    }

    [Theory]
    [InlineData(TripRequestStatus.Pending)]
    [InlineData(TripRequestStatus.ProposalsReceived)]
    public async Task GetItineraryTrackingAsync_Fails_ForPreMatchStatuses(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, tripRequest, proposal, _, _) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = status;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.GetItineraryTrackingAsync(tourist.Id, proposal.Id);

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task GetItineraryTrackingAsync_Fails_ForUnauthorizedUser()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, _, _, proposal, _, _) = await SeedTripWithItineraryAsync(context);

        var stranger = new User
        {
            Id = 999,
            FullName = "Stranger",
            Email = "stranger@test.com",
            PhoneNumber = "01700000999",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };
        context.Users.Add(stranger);
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.GetItineraryTrackingAsync(stranger.Id, proposal.Id);

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task GetItineraryTrackingAsync_Fails_ForInactiveUser()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal, _, _) = await SeedTripWithItineraryAsync(context);

        tourist.IsActive = false;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.GetItineraryTrackingAsync(tourist.Id, proposal.Id);

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
    }

    [Theory]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.InProgress)]
    public async Task UpdateActivityStatusAsync_Succeeds_DuringPostMatchActiveStatuses(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, _, activity) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = status;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.UpdateActivityStatusAsync(guide.Id, activity.Id, "IN_PROGRESS");

        Assert.True(result.Success);
        Assert.Equal(ItineraryTrackingError.None, result.Error);
        Assert.Equal("IN_PROGRESS", result.Data!.Status);

        var stored = await context.ItineraryActivities.FindAsync(activity.Id);
        Assert.Equal(ActivityStatus.InProgress, stored!.Status);
    }

    [Fact]
    public async Task UpdateActivityStatusAsync_Fails_ForCompletedTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, _, activity) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = TripRequestStatus.Completed;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.UpdateActivityStatusAsync(guide.Id, activity.Id, "COMPLETED");

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);

        var stored = await context.ItineraryActivities.FindAsync(activity.Id);
        Assert.Equal(ActivityStatus.Upcoming, stored!.Status);
    }

    [Fact]
    public async Task UpdateActivityStatusAsync_Fails_ForCancelledTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, _, activity) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = TripRequestStatus.Cancelled;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.UpdateActivityStatusAsync(guide.Id, activity.Id, "IN_PROGRESS");

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task UpdateActivityStatusAsync_Fails_ForUnauthorizedGuide()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, _, _, _, _, activity) = await SeedTripWithItineraryAsync(context);

        var otherGuide = new User
        {
            Id = 888,
            FullName = "Other Guide",
            Email = "otherguide@test.com",
            PhoneNumber = "01700000888",
            PasswordHash = "hash",
            Role = UserRole.Guide,
            IsActive = true
        };
        context.Users.Add(otherGuide);
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.UpdateActivityStatusAsync(otherGuide.Id, activity.Id, "IN_PROGRESS");

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
    }

    [Theory]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.InProgress)]
    public async Task CreateActivityAsync_Succeeds_DuringPostMatchActiveStatuses(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, day, _) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = status;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.CreateActivityAsync(guide.Id, new CreateItineraryActivityDto
        {
            ItineraryDayId = day.Id,
            Title = "New activity",
            Description = "Activity description",
            OrderIndex = 2
        });

        Assert.True(result.Success);
        Assert.Equal(ItineraryTrackingError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal("New activity", result.Data.Title);
    }

    [Fact]
    public async Task CreateActivityAsync_Fails_ForCompletedTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, day, _) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = TripRequestStatus.Completed;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.CreateActivityAsync(guide.Id, new CreateItineraryActivityDto
        {
            ItineraryDayId = day.Id,
            Title = "New activity",
            Description = "Activity description",
            OrderIndex = 2
        });

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task CreateActivityAsync_Fails_ForCancelledTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, day, _) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = TripRequestStatus.Cancelled;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.CreateActivityAsync(guide.Id, new CreateItineraryActivityDto
        {
            ItineraryDayId = day.Id,
            Title = "New activity",
            Description = "Activity description",
            OrderIndex = 2
        });

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
    }

    [Theory]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.InProgress)]
    public async Task DeleteActivityAsync_Succeeds_DuringPostMatchActiveStatuses(TripRequestStatus status)
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, _, activity) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = status;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.DeleteActivityAsync(guide.Id, activity.Id);

        Assert.True(result.Success);
        Assert.Equal(ItineraryTrackingError.None, result.Error);
        Assert.Null(await context.ItineraryActivities.FindAsync(activity.Id));
    }

    [Fact]
    public async Task DeleteActivityAsync_Fails_ForCompletedTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, _, activity) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = TripRequestStatus.Completed;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.DeleteActivityAsync(guide.Id, activity.Id);

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
        Assert.NotNull(await context.ItineraryActivities.FindAsync(activity.Id));
    }

    [Fact]
    public async Task DeleteActivityAsync_Fails_ForCancelledTrip()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest, _, _, activity) = await SeedTripWithItineraryAsync(context);

        tripRequest.Status = TripRequestStatus.Cancelled;
        await context.SaveChangesAsync();

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        var result = await service.DeleteActivityAsync(guide.Id, activity.Id);

        Assert.False(result.Success);
        Assert.Equal(ItineraryTrackingError.Unauthorized, result.Error);
        Assert.NotNull(await context.ItineraryActivities.FindAsync(activity.Id));
    }

    [Fact]
    public async Task IsProposalParticipantAsync_DistinguishesActiveVsHistorical()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, guide, tripRequest, proposal, _, _) = await SeedTripWithItineraryAsync(context);

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());

        // Active status
        tripRequest.Status = TripRequestStatus.InProgress;
        await context.SaveChangesAsync();
        Assert.True(await service.IsProposalParticipantAsync(tourist.Id, proposal.Id, allowHistorical: false));
        Assert.True(await service.IsProposalParticipantAsync(guide.Id, proposal.Id, allowHistorical: false));

        // Completed status: active false, historical true
        tripRequest.Status = TripRequestStatus.Completed;
        await context.SaveChangesAsync();
        Assert.False(await service.IsProposalParticipantAsync(tourist.Id, proposal.Id, allowHistorical: false));
        Assert.False(await service.IsProposalParticipantAsync(guide.Id, proposal.Id, allowHistorical: false));
        Assert.True(await service.IsProposalParticipantAsync(tourist.Id, proposal.Id, allowHistorical: true));
        Assert.True(await service.IsProposalParticipantAsync(guide.Id, proposal.Id, allowHistorical: true));

        // Cancelled status: both false
        tripRequest.Status = TripRequestStatus.Cancelled;
        await context.SaveChangesAsync();
        Assert.False(await service.IsProposalParticipantAsync(tourist.Id, proposal.Id, allowHistorical: false));
        Assert.False(await service.IsProposalParticipantAsync(tourist.Id, proposal.Id, allowHistorical: true));
    }

    [Fact]
    public async Task Controller_GetItineraryTracking_Returns200_WhenAuthorized()
    {
        await using var context = CreateInMemoryDbContext();
        var (tourist, _, _, proposal, _, _) = await SeedTripWithItineraryAsync(context);

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());
        var hubContext = new FakeItineraryTrackingHubContext();
        var controller = new ItineraryTrackingController(service, hubContext, context, new LoggerFactory().CreateLogger<ItineraryTrackingController>())
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

        var response = await controller.GetItineraryTracking(proposal.Id);
        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task Controller_GetItineraryTracking_Returns401_WhenUnauthenticated()
    {
        await using var context = CreateInMemoryDbContext();
        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());
        var hubContext = new FakeItineraryTrackingHubContext();
        var controller = new ItineraryTrackingController(service, hubContext, context, new LoggerFactory().CreateLogger<ItineraryTrackingController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var response = await controller.GetItineraryTracking(100);
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Controller_UpdateActivityStatus_BroadcastsActivityStatusUpdated_AndReturns200()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, _, proposal, _, activity) = await SeedTripWithItineraryAsync(context);

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());
        var hubContext = new FakeItineraryTrackingHubContext();
        var controller = new ItineraryTrackingController(service, hubContext, context, new LoggerFactory().CreateLogger<ItineraryTrackingController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, guide.Id.ToString()),
                        new Claim(ClaimTypes.Role, "Guide")
                    }, "TestAuth"))
                }
            }
        };

        var response = await controller.UpdateActivityStatus(activity.Id, new UpdateActivityStatusDto
        {
            Status = "IN_PROGRESS"
        });

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        // Verify broadcast to group
        Assert.Contains(hubContext.HubClients.GroupProxy.Invocations, inv =>
            inv.method == "ActivityStatusUpdated" &&
            inv.args[0] is ItineraryActivityDto dto &&
            dto.Status == "IN_PROGRESS" &&
            dto.ProposalId == proposal.Id);
    }

    [Fact]
    public async Task Controller_CreateActivity_BroadcastsActivityCreated_AndReturns201()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, _, proposal, day, _) = await SeedTripWithItineraryAsync(context);

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());
        var hubContext = new FakeItineraryTrackingHubContext();
        var controller = new ItineraryTrackingController(service, hubContext, context, new LoggerFactory().CreateLogger<ItineraryTrackingController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, guide.Id.ToString()),
                        new Claim(ClaimTypes.Role, "Guide")
                    }, "TestAuth"))
                }
            }
        };

        var response = await controller.CreateActivity(new CreateItineraryActivityDto
        {
            ItineraryDayId = day.Id,
            Title = "Sunset Boat Ride",
            Description = "Boat ride on the lake",
            OrderIndex = 2
        });

        var createdResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);

        // Verify broadcast to group
        Assert.Contains(hubContext.HubClients.GroupProxy.Invocations, inv =>
            inv.method == "ActivityCreated" &&
            inv.args[0] is ItineraryActivityDto dto &&
            dto.Title == "Sunset Boat Ride" &&
            dto.ProposalId == proposal.Id);
    }

    [Fact]
    public async Task Controller_DeleteActivity_BroadcastsActivityDeleted_AndReturns200()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, _, _, _, activity) = await SeedTripWithItineraryAsync(context);

        var service = new ItineraryTrackingService(context, new LoggerFactory().CreateLogger<ItineraryTrackingService>());
        var hubContext = new FakeItineraryTrackingHubContext();
        var controller = new ItineraryTrackingController(service, hubContext, context, new LoggerFactory().CreateLogger<ItineraryTrackingController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, guide.Id.ToString()),
                        new Claim(ClaimTypes.Role, "Guide")
                    }, "TestAuth"))
                }
            }
        };

        var response = await controller.DeleteActivity(activity.Id);
        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        // Verify broadcast to group
        Assert.Contains(hubContext.HubClients.GroupProxy.Invocations, inv =>
            inv.method == "ActivityDeleted");
    }
}

public class FakeItineraryTrackingHubContext : IHubContext<ItineraryTrackingHub>
{
    public FakeTrackingHubClients HubClients { get; } = new();
    public IHubClients Clients => HubClients;
    public IGroupManager Groups { get; } = new FakeGroupManager();
}

public class FakeTrackingHubClients : IHubClients
{
    public TestClientProxy GroupProxy { get; } = new();

    public IClientProxy All => new TestClientProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy();
    public IClientProxy Client(string connectionId) => new TestClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new TestClientProxy();
    public IClientProxy Group(string groupName) => GroupProxy;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => GroupProxy;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => GroupProxy;
    public IClientProxy User(string userId) => new TestClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new TestClientProxy();
}

