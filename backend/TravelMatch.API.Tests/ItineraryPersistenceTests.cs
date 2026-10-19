using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Itineraries;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

public class ItineraryPersistenceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static NotificationService BuildNotificationService(ApplicationDbContext ctx)
    {
        var logger = new LoggerFactory().CreateLogger<NotificationService>();
        return new NotificationService(ctx, logger, new NoopNotificationSender());
    }

    private static ItineraryService BuildItineraryService(ApplicationDbContext ctx)
    {
        var logger = new LoggerFactory().CreateLogger<ItineraryService>();
        var notificationService = BuildNotificationService(ctx);
        return new ItineraryService(ctx, logger, notificationService);
    }

    private static async Task<(User tourist, User guide, TripRequest tripRequest)> SeedPendingTripAsync(ApplicationDbContext context)
    {
        var tourist = new User
        {
            Id = 101,
            FullName = "Test Tourist",
            Email = "tourist101@test.com",
            PhoneNumber = "01711111101",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var guide = new User
        {
            Id = 201,
            FullName = "Test Guide",
            Email = "guide201@test.com",
            PhoneNumber = "01711111201",
            PasswordHash = "hash",
            Role = UserRole.Guide,
            IsActive = true
        };

        var tripRequest = new TripRequest
        {
            Id = 501,
            TouristId = tourist.Id,
            Destination = "Cox's Bazar",
            TripType = "Beach",
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            Status = TripRequestStatus.Pending
        };

        context.Users.AddRange(tourist, guide);
        context.TripRequests.Add(tripRequest);
        await context.SaveChangesAsync();

        return (tourist, guide, tripRequest);
    }

    [Fact]
    public async Task CreateProposalAsync_PersistsProposal_AndTransitionsTripStatus()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var service = BuildItineraryService(context);

        var request = new CreateProposalDto
        {
            ProposedPrice = 18000m,
            Message = "Comprehensive 2-day tour"
        };

        var result = await service.CreateProposalAsync(guide.Id, tripRequest.Id, request);

        Assert.True(result.Success);
        Assert.Equal(ItineraryError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal(18000m, result.Data.ProposedPrice);

        // Verify PostgreSQL / EF Core persistence
        var storedProposal = await context.Proposals
            .FirstOrDefaultAsync(p => p.TripRequestId == tripRequest.Id && p.GuideId == guide.Id);

        Assert.NotNull(storedProposal);
        Assert.Equal(18000m, storedProposal.ProposedPrice);
        Assert.Equal(ProposalStatus.Draft, storedProposal.Status);

        // Trip request status remains Pending while proposal is in Draft
        var updatedTrip = await context.TripRequests.FindAsync(tripRequest.Id);
        Assert.Equal(TripRequestStatus.Pending, updatedTrip!.Status);
    }

    [Fact]
    public async Task CreateProposalAsync_Fails_WhenTripRequestNotPendingOrProposalsReceived()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        tripRequest.Status = TripRequestStatus.Matched;
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);

        var result = await service.CreateProposalAsync(guide.Id, tripRequest.Id, new CreateProposalDto
        {
            ProposedPrice = 15000m,
            Message = "Too late"
        });

        Assert.False(result.Success);
        Assert.Equal(ItineraryError.ProposalNotFound, result.Error);
    }

    [Fact]
    public async Task AddDayAsync_PersistsNewDay_WhenAuthorized()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 601,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Submitted
        };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);

        var result = await service.AddDayAsync(guide.Id, proposal.Id, new CreateItineraryDayDto
        {
            DayNumber = 1,
            Title = "Welcome Dinner",
            Activities = "Dinner at seafood restaurant",
            Schedule = "7:00 PM"
        });

        Assert.True(result.Success);
        Assert.Equal(ItineraryError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal("Welcome Dinner", result.Data.Title);

        var storedDay = await context.ItineraryDays.SingleOrDefaultAsync(d => d.ProposalId == proposal.Id);
        Assert.NotNull(storedDay);
        Assert.Equal(1, storedDay.DayNumber);
    }

    [Fact]
    public async Task AddDayAsync_RejectsDuplicateDayNumber()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 602,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Submitted
        };
        context.Proposals.Add(proposal);
        context.ItineraryDays.Add(new ItineraryDay
        {
            ProposalId = proposal.Id,
            DayNumber = 1,
            Title = "Existing Day 1",
            Activities = "Existing activities"
        });
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);

        var result = await service.AddDayAsync(guide.Id, proposal.Id, new CreateItineraryDayDto
        {
            DayNumber = 1,
            Title = "Duplicate Day 1",
            Activities = "New activities"
        });

        Assert.False(result.Success);
        Assert.Equal(ItineraryError.DuplicateDayNumber, result.Error);
    }

    [Fact]
    public async Task AddDayAsync_RejectsNonOwnerGuide()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 603,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Submitted
        };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);

        var result = await service.AddDayAsync(guideUserId: 9999, proposal.Id, new CreateItineraryDayDto
        {
            DayNumber = 1,
            Title = "Intruder Day",
            Activities = "Hacking"
        });

        Assert.False(result.Success);
        Assert.Equal(ItineraryError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task AddDayAsync_RejectsRejectedProposal()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 604,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Rejected
        };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);

        var result = await service.AddDayAsync(guide.Id, proposal.Id, new CreateItineraryDayDto
        {
            DayNumber = 1,
            Title = "Post-Rejection Day",
            Activities = "Activities"
        });

        Assert.False(result.Success);
        Assert.Equal(ItineraryError.CannotEditRejectedProposal, result.Error);
    }

    [Fact]
    public async Task UpdateDayAsync_PersistsChanges_WhenAuthorized()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 605,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Submitted
        };
        var day = new ItineraryDay
        {
            Id = 701,
            ProposalId = proposal.Id,
            DayNumber = 1,
            Title = "Original Title",
            Activities = "Original Activities"
        };
        context.Proposals.Add(proposal);
        context.ItineraryDays.Add(day);
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);

        var result = await service.UpdateDayAsync(guide.Id, day.Id, new UpdateItineraryDayDto
        {
            Title = "Updated Title",
            Activities = "Updated Activities",
            Schedule = "Afternoon"
        });

        Assert.True(result.Success);
        Assert.Equal(ItineraryError.None, result.Error);
        Assert.Equal("Updated Title", result.Data!.Title);

        var stored = await context.ItineraryDays.FindAsync(day.Id);
        Assert.Equal("Updated Title", stored!.Title);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task DeleteDayAsync_RemovesDay_WhenAuthorized()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 606,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Submitted
        };
        var day = new ItineraryDay
        {
            Id = 702,
            ProposalId = proposal.Id,
            DayNumber = 1,
            Title = "To Be Deleted",
            Activities = "Activities"
        };
        context.Proposals.Add(proposal);
        context.ItineraryDays.Add(day);
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);

        var result = await service.DeleteDayAsync(guide.Id, day.Id);

        Assert.True(result.Success);
        Assert.Equal(ItineraryError.None, result.Error);
        Assert.Null(await context.ItineraryDays.FindAsync(day.Id));
    }

    [Fact]
    public async Task SaveItineraryAsync_BulkReplacesDays_Correctly()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 607,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Submitted
        };
        context.Proposals.Add(proposal);
        context.ItineraryDays.Add(new ItineraryDay
        {
            ProposalId = proposal.Id,
            DayNumber = 1,
            Title = "Old Day 1",
            Activities = "Old Activities"
        });
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);

        var newDays = new List<CreateItineraryDayDto>
        {
            new() { DayNumber = 1, Title = "Fresh Day 1", Activities = "Fresh Activities 1" },
            new() { DayNumber = 2, Title = "Fresh Day 2", Activities = "Fresh Activities 2" }
        };

        var result = await service.SaveItineraryAsync(guide.Id, proposal.Id, newDays);

        Assert.True(result.Success);

        var storedDays = await context.ItineraryDays.Where(d => d.ProposalId == proposal.Id).OrderBy(d => d.DayNumber).ToListAsync();
        Assert.Equal(2, storedDays.Count);
        Assert.Equal("Fresh Day 1", storedDays[0].Title);
        Assert.Equal("Fresh Day 2", storedDays[1].Title);
    }

    [Fact]
    public async Task ItineraryController_Returns401_WhenUserNotAuthenticated()
    {
        await using var context = CreateInMemoryDbContext();
        var service = BuildItineraryService(context);
        var controller = new ItineraryController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetProposal(100);
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task ItineraryController_Returns403_WhenGuideNotOwner()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 608,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Submitted
        };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);
        var controller = new ItineraryController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "9999"),
                        new Claim(ClaimTypes.Role, "Guide")
                    }, "TestAuth"))
                }
            }
        };

        var result = await controller.GetProposal(proposal.Id);
        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task ItineraryController_Returns200_WhenAuthorizedGuide()
    {
        await using var context = CreateInMemoryDbContext();
        var (_, guide, tripRequest) = await SeedPendingTripAsync(context);

        var proposal = new Proposal
        {
            Id = 609,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 12000m,
            Availability = "Available",
            Status = ProposalStatus.Submitted
        };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var service = BuildItineraryService(context);
        var controller = new ItineraryController(service)
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

        var result = await controller.GetProposal(proposal.Id);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }
}
