using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for <see cref="TripCostCalculator"/>, <see cref="TripCostService"/>,
/// and <see cref="CostBreakdownController"/>.
///
/// Coverage:
///   • Normal calculations (proposal flow, registration flow, estimate)
///   • Boundary values (1 traveler, price exactly = BudgetMax, large traveler counts)
///   • Invalid / guard inputs (null, zero/negative price, zero travelers)
///   • Rounding of PricePerPerson (indivisible group price)
///   • TripCostService authorization rules
///   • Controller HTTP status mapping
/// </summary>
public class TripCostCalculatorTests
{
    // ─── DB helpers ──────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TripCostService BuildService(ApplicationDbContext ctx) =>
        new(ctx);

    // ─── Seed helpers ────────────────────────────────────────────────────────

    private static async Task<(User tourist, User guide, TripRequest tripRequest, Proposal proposal)>
        SeedProposalAsync(
            ApplicationDbContext ctx,
            decimal proposedPrice = 600m,
            int numberOfTravelers = 3,
            ProposalStatus status = ProposalStatus.Accepted)
    {
        var tourist = new User
        {
            Id = 1, FullName = "Tourist A", Email = "ta@test.com",
            PhoneNumber = "01711000001", Role = UserRole.Tourist, IsActive = true
        };
        var guide = new User
        {
            Id = 2, FullName = "Guide B", Email = "gb@test.com",
            PhoneNumber = "01722000002", Role = UserRole.Guide, IsActive = true
        };
        var tripRequest = new TripRequest
        {
            Id = 10, TouristId = tourist.Id, Destination = "Sylhet",
            TripType = "Adventure",
            StartDate = new DateOnly(2026, 12, 1),
            EndDate   = new DateOnly(2026, 12, 5),
            NumberOfTravelers = numberOfTravelers,
            Budget = 700m, BudgetMin = 400m, BudgetMax = 750m,
            Status = TripRequestStatus.Matched, MatchedGuideId = guide.Id
        };
        var proposal = new Proposal
        {
            Id = 100, TripRequestId = tripRequest.Id, TripRequest = tripRequest,
            GuideId = guide.Id, ProposedPrice = proposedPrice,
            Inclusions = "Breakfast, Guide fee",
            Exclusions = "Flights",
            Status = status
        };

        ctx.Users.AddRange(tourist, guide);
        ctx.TripRequests.Add(tripRequest);
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();
        return (tourist, guide, tripRequest, proposal);
    }

    private static async Task<(User tourist, TouristProfile touristProfile, OrganizedTrip trip, Registration registration)>
        SeedRegistrationAsync(
            ApplicationDbContext ctx,
            decimal pricePerPerson = 200m,
            int numberOfTravelers = 2,
            int? registrationId = null)
    {
        var orgUser = new User
        {
            Id = 20, FullName = "Organizer C", Email = "oc@test.com",
            PhoneNumber = "01733000003", Role = UserRole.Organizer, IsActive = true
        };
        var orgProfile = new OrganizerProfile
        {
            Id = 200, UserId = orgUser.Id, GroupOrganizationName = "Trek Co"
        };
        var touristUser = new User
        {
            Id = 30, FullName = "Tourist D", Email = "td@test.com",
            PhoneNumber = "01744000004", Role = UserRole.Tourist, IsActive = true
        };
        var touristProfile = new TouristProfile
        {
            Id = 300, UserId = touristUser.Id, Preferences = "Culture"
        };
        var trip = new OrganizedTrip
        {
            Id = 500, OrganizerId = orgProfile.Id,
            Title = "Sundarbans Explorer",
            Destination = "Sundarbans",
            Description = "Mangrove tour",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            EndDate   = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(24)),
            MaxParticipants = 10,
            PricePerPerson = pricePerPerson,
            Inclusions = "Boat, Meals",
            Exclusions = "Personal expenses",
            RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            Status = OrganizedTripStatus.RegistrationOpen
        };
        var registration = new Registration
        {
            Id = registrationId ?? 800,
            OrganizedTripId = trip.Id,
            TouristId = touristProfile.Id,
            NumberOfTravelers = numberOfTravelers,
            TotalPrice = pricePerPerson * numberOfTravelers,
            Status = RegistrationStatus.PendingPayment,
            RegisteredAt = DateTime.UtcNow
        };

        ctx.Users.AddRange(orgUser, touristUser);
        ctx.OrganizerProfiles.Add(orgProfile);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.OrganizedTrips.Add(trip);
        ctx.Registrations.Add(registration);
        await ctx.SaveChangesAsync();
        return (touristUser, touristProfile, trip, registration);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TripCostCalculator — static unit tests (no DB)
    // ═══════════════════════════════════════════════════════════════════════════

    // ── Proposal path ─────────────────────────────────────────────────────────

    [Fact]
    public void CalculateForProposal_NormalCase_ReturnsCorrectBreakdown()
    {
        var proposal = new Proposal
        {
            Id = 1, TripRequestId = 10, GuideId = 2,
            ProposedPrice = 600m,
            Inclusions = "Guide fee", Exclusions = "Flights",
            TripRequest = new TripRequest { Id = 10, TouristId = 1, NumberOfTravelers = 3 }
        };

        var result = TripCostCalculator.CalculateForProposal(proposal);

        Assert.Equal(CostBreakdownType.Proposal, result.BreakdownType);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(1, result.ProposalId);
        Assert.Equal(10, result.TripRequestId);
        Assert.Equal(600m, result.BasePrice);
        Assert.Equal(3, result.NumberOfTravelers);
        Assert.Equal(200m, result.PricePerPerson);     // 600 / 3
        Assert.Equal(600m, result.TotalAmount);         // flat group price
        Assert.Equal("Guide fee", result.Inclusions);
        Assert.Equal("Flights", result.Exclusions);
    }

    [Fact]
    public void CalculateForProposal_OneTraveler_PerPersonEqualsBasePrice()
    {
        var proposal = new Proposal
        {
            Id = 1, TripRequestId = 10, GuideId = 2,
            ProposedPrice = 350m,
            TripRequest = new TripRequest { Id = 10, TouristId = 1, NumberOfTravelers = 1 }
        };

        var result = TripCostCalculator.CalculateForProposal(proposal);

        Assert.Equal(350m, result.PricePerPerson);
        Assert.Equal(350m, result.TotalAmount);
    }

    [Fact]
    public void CalculateForProposal_IndivisiblePrice_RoundsPerPersonAwayFromZero()
    {
        // 100 / 3 = 33.3333… → rounds to 33.33
        var proposal = new Proposal
        {
            Id = 1, TripRequestId = 10, GuideId = 2,
            ProposedPrice = 100m,
            TripRequest = new TripRequest { Id = 10, TouristId = 1, NumberOfTravelers = 3 }
        };

        var result = TripCostCalculator.CalculateForProposal(proposal);

        Assert.Equal(33.33m, result.PricePerPerson);
        // TotalAmount is always the full group price, not per-person × travelers.
        Assert.Equal(100m, result.TotalAmount);
    }

    [Fact]
    public void CalculateForProposal_LargeNumberOfTravelers_ComputesCorrectly()
    {
        // 10,000 / 100 = 100.00
        var proposal = new Proposal
        {
            Id = 1, TripRequestId = 10, GuideId = 2,
            ProposedPrice = 10_000m,
            TripRequest = new TripRequest { Id = 10, TouristId = 1, NumberOfTravelers = 100 }
        };

        var result = TripCostCalculator.CalculateForProposal(proposal);

        Assert.Equal(100m, result.PricePerPerson);
        Assert.Equal(10_000m, result.TotalAmount);
    }

    [Fact]
    public void CalculateForProposal_NullProposal_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TripCostCalculator.CalculateForProposal(null!));
    }

    [Fact]
    public void CalculateForProposal_ZeroPrice_ThrowsArgumentException()
    {
        var proposal = new Proposal
        {
            ProposedPrice = 0m,
            TripRequest = new TripRequest { NumberOfTravelers = 2 }
        };

        Assert.Throws<ArgumentException>(() =>
            TripCostCalculator.CalculateForProposal(proposal));
    }

    [Fact]
    public void CalculateForProposal_NegativePrice_ThrowsArgumentException()
    {
        var proposal = new Proposal
        {
            ProposedPrice = -50m,
            TripRequest = new TripRequest { NumberOfTravelers = 2 }
        };

        Assert.Throws<ArgumentException>(() =>
            TripCostCalculator.CalculateForProposal(proposal));
    }

    [Fact]
    public void CalculateForProposal_ZeroTravelers_ThrowsArgumentException()
    {
        var proposal = new Proposal
        {
            ProposedPrice = 500m,
            TripRequest = new TripRequest { NumberOfTravelers = 0 }
        };

        Assert.Throws<ArgumentException>(() =>
            TripCostCalculator.CalculateForProposal(proposal));
    }

    [Fact]
    public void CalculateForProposal_NullTripRequest_ThrowsArgumentException()
    {
        var proposal = new Proposal { ProposedPrice = 500m, TripRequest = null! };

        // TripRequest is null → NumberOfTravelers resolves to 0 → ArgumentException.
        Assert.Throws<ArgumentException>(() =>
            TripCostCalculator.CalculateForProposal(proposal));
    }

    // ── Registration path ─────────────────────────────────────────────────────

    [Fact]
    public void CalculateForRegistration_NormalCase_ReturnsCorrectBreakdown()
    {
        var trip = new OrganizedTrip
        {
            Id = 500, PricePerPerson = 200m,
            Inclusions = "Boat, Meals", Exclusions = "Personal expenses"
        };

        var result = TripCostCalculator.CalculateForRegistration(trip, numberOfTravelers: 2, registrationId: 800);

        Assert.Equal(CostBreakdownType.Registration, result.BreakdownType);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(500, result.OrganizedTripId);
        Assert.Equal(800, result.RegistrationId);
        Assert.Equal(200m, result.BasePrice);
        Assert.Equal(2, result.NumberOfTravelers);
        Assert.Equal(200m, result.PricePerPerson);   // already per-person
        Assert.Equal(400m, result.TotalAmount);       // 200 × 2
        Assert.Equal("Boat, Meals", result.Inclusions);
        Assert.Equal("Personal expenses", result.Exclusions);
    }

    [Fact]
    public void CalculateForRegistration_OneTraveler_TotalEqualsBasePrice()
    {
        var trip = new OrganizedTrip { Id = 500, PricePerPerson = 175m, Inclusions = "" };

        var result = TripCostCalculator.CalculateForRegistration(trip, numberOfTravelers: 1);

        Assert.Equal(175m, result.TotalAmount);
        Assert.Equal(175m, result.PricePerPerson);
    }

    [Fact]
    public void CalculateForRegistration_LargeTravelerCount_TotalIsCorrect()
    {
        var trip = new OrganizedTrip { Id = 500, PricePerPerson = 99.50m, Inclusions = "" };

        var result = TripCostCalculator.CalculateForRegistration(trip, numberOfTravelers: 10);

        Assert.Equal(995.00m, result.TotalAmount);
    }

    [Fact]
    public void CalculateForRegistration_NullOrganizedTrip_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TripCostCalculator.CalculateForRegistration(null!, 2));
    }

    [Fact]
    public void CalculateForRegistration_ZeroPrice_ThrowsArgumentException()
    {
        var trip = new OrganizedTrip { PricePerPerson = 0m, Inclusions = "" };
        Assert.Throws<ArgumentException>(() =>
            TripCostCalculator.CalculateForRegistration(trip, 2));
    }

    [Fact]
    public void CalculateForRegistration_NegativePrice_ThrowsArgumentException()
    {
        var trip = new OrganizedTrip { PricePerPerson = -100m, Inclusions = "" };
        Assert.Throws<ArgumentException>(() =>
            TripCostCalculator.CalculateForRegistration(trip, 2));
    }

    [Fact]
    public void CalculateForRegistration_ZeroTravelers_ThrowsArgumentOutOfRangeException()
    {
        var trip = new OrganizedTrip { PricePerPerson = 100m, Inclusions = "" };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TripCostCalculator.CalculateForRegistration(trip, numberOfTravelers: 0));
    }

    [Fact]
    public void CalculateForRegistration_NegativeTravelers_ThrowsArgumentOutOfRangeException()
    {
        var trip = new OrganizedTrip { PricePerPerson = 100m, Inclusions = "" };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TripCostCalculator.CalculateForRegistration(trip, numberOfTravelers: -1));
    }

    [Fact]
    public void CalculateForRegistration_NoRegistrationId_LeavesRegistrationIdNull()
    {
        var trip = new OrganizedTrip { Id = 5, PricePerPerson = 100m, Inclusions = "" };
        var result = TripCostCalculator.CalculateForRegistration(trip, numberOfTravelers: 2);
        Assert.Null(result.RegistrationId);
    }

    [Fact]
    public void CalculateForRegistration_NullExclusions_ReturnsEmptyString()
    {
        var trip = new OrganizedTrip { Id = 5, PricePerPerson = 100m, Inclusions = "Meals", Exclusions = null };
        var result = TripCostCalculator.CalculateForRegistration(trip, numberOfTravelers: 1);
        Assert.Equal(string.Empty, result.Exclusions);
    }

    // ── Consistency with existing RegistrationService formula ────────────────

    [Fact]
    public void CalculateForRegistration_TotalMatchesRegistrationServiceFormula()
    {
        // RegistrationService sets TotalPrice = organizedTrip.PricePerPerson * request.NumberOfTravelers.
        // The calculator must produce the same result.
        decimal pricePerPerson = 250m;
        int travelers = 4;
        var trip = new OrganizedTrip { Id = 1, PricePerPerson = pricePerPerson, Inclusions = "" };

        var result = TripCostCalculator.CalculateForRegistration(trip, travelers);

        Assert.Equal(pricePerPerson * travelers, result.TotalAmount);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TripCostService — DB-backed integration tests
    // ═══════════════════════════════════════════════════════════════════════════

    // ── GetProposalBreakdownAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetProposalBreakdown_TouristOwner_ReturnsCorrectBreakdown()
    {
        var ctx = BuildContext();
        var (tourist, _, tripRequest, proposal) = await SeedProposalAsync(ctx, proposedPrice: 900m, numberOfTravelers: 3);
        var svc = BuildService(ctx);

        var result = await svc.GetProposalBreakdownAsync(proposal.Id, tourist.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(CostBreakdownType.Proposal, result.Data.BreakdownType);
        Assert.Equal(900m, result.Data.BasePrice);
        Assert.Equal(900m, result.Data.TotalAmount);
        Assert.Equal(300m, result.Data.PricePerPerson);   // 900 / 3
        Assert.Equal(3, result.Data.NumberOfTravelers);
    }

    [Fact]
    public async Task GetProposalBreakdown_GuideOwner_ReturnsCorrectBreakdown()
    {
        var ctx = BuildContext();
        var (_, guide, _, proposal) = await SeedProposalAsync(ctx, proposedPrice: 600m, numberOfTravelers: 2);
        var svc = BuildService(ctx);

        var result = await svc.GetProposalBreakdownAsync(proposal.Id, guide.Id);

        Assert.True(result.Success);
        Assert.Equal(300m, result.Data!.PricePerPerson);  // 600 / 2
    }

    [Fact]
    public async Task GetProposalBreakdown_UnauthorizedUser_ReturnsForbidden()
    {
        var ctx = BuildContext();
        var (_, _, _, proposal) = await SeedProposalAsync(ctx);
        var svc = BuildService(ctx);

        const int unrelatedUserId = 999;
        var result = await svc.GetProposalBreakdownAsync(proposal.Id, unrelatedUserId);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task GetProposalBreakdown_ProposalNotFound_ReturnsNotFound()
    {
        var ctx = BuildContext();
        var svc = BuildService(ctx);

        var result = await svc.GetProposalBreakdownAsync(proposalId: 9999, requestingUserId: 1);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.ProposalNotFound, result.Error);
    }

    [Fact]
    public async Task GetProposalBreakdown_InvalidId_ReturnsInvalidRequest()
    {
        var ctx = BuildContext();
        var svc = BuildService(ctx);

        var result = await svc.GetProposalBreakdownAsync(proposalId: 0, requestingUserId: 1);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.InvalidRequest, result.Error);
    }

    [Fact]
    public async Task GetProposalBreakdown_NegativeId_ReturnsInvalidRequest()
    {
        var ctx = BuildContext();
        var svc = BuildService(ctx);

        var result = await svc.GetProposalBreakdownAsync(proposalId: -5, requestingUserId: 1);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.InvalidRequest, result.Error);
    }

    // ── GetRegistrationBreakdownAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetRegistrationBreakdown_TouristOwner_ReturnsCorrectBreakdown()
    {
        var ctx = BuildContext();
        var (tourist, _, trip, registration) =
            await SeedRegistrationAsync(ctx, pricePerPerson: 150m, numberOfTravelers: 4);
        var svc = BuildService(ctx);

        var result = await svc.GetRegistrationBreakdownAsync(registration.Id, tourist.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(CostBreakdownType.Registration, result.Data.BreakdownType);
        Assert.Equal(150m, result.Data.BasePrice);
        Assert.Equal(150m, result.Data.PricePerPerson);
        Assert.Equal(600m, result.Data.TotalAmount);   // 150 × 4
        Assert.Equal(4, result.Data.NumberOfTravelers);
        Assert.Equal(registration.Id, result.Data.RegistrationId);
    }

    [Fact]
    public async Task GetRegistrationBreakdown_TotalMatchesStoredTotalPrice()
    {
        var ctx = BuildContext();
        var (tourist, _, _, registration) =
            await SeedRegistrationAsync(ctx, pricePerPerson: 250m, numberOfTravelers: 3);
        var svc = BuildService(ctx);

        var result = await svc.GetRegistrationBreakdownAsync(registration.Id, tourist.Id);

        Assert.True(result.Success);
        // Stored TotalPrice = PricePerPerson * NumberOfTravelers; calculator must match.
        Assert.Equal(registration.TotalPrice, result.Data!.TotalAmount);
    }

    [Fact]
    public async Task GetRegistrationBreakdown_UnauthorizedUser_ReturnsForbidden()
    {
        var ctx = BuildContext();
        var (_, _, _, registration) = await SeedRegistrationAsync(ctx);
        var svc = BuildService(ctx);

        // Seed an unrelated tourist
        var other = new User
        {
            Id = 99, FullName = "Other", Email = "other@test.com",
            PhoneNumber = "01799000099", Role = UserRole.Tourist, IsActive = true
        };
        var otherProfile = new TouristProfile { Id = 990, UserId = other.Id };
        ctx.Users.Add(other);
        ctx.TouristProfiles.Add(otherProfile);
        await ctx.SaveChangesAsync();

        var result = await svc.GetRegistrationBreakdownAsync(registration.Id, other.Id);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task GetRegistrationBreakdown_RegistrationNotFound_ReturnsNotFound()
    {
        var ctx = BuildContext();
        // Seed tourist profile so the service reaches the lookup step
        var user = new User
        {
            Id = 1, FullName = "T", Email = "t@t.com",
            PhoneNumber = "01700000001", Role = UserRole.Tourist, IsActive = true
        };
        ctx.Users.Add(user);
        ctx.TouristProfiles.Add(new TouristProfile { Id = 1, UserId = user.Id });
        await ctx.SaveChangesAsync();
        var svc = BuildService(ctx);

        var result = await svc.GetRegistrationBreakdownAsync(registrationId: 9999, requestingUserId: user.Id);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.RegistrationNotFound, result.Error);
    }

    [Fact]
    public async Task GetRegistrationBreakdown_InvalidId_ReturnsInvalidRequest()
    {
        var ctx = BuildContext();
        var svc = BuildService(ctx);

        var result = await svc.GetRegistrationBreakdownAsync(registrationId: 0, requestingUserId: 1);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.InvalidRequest, result.Error);
    }

    [Fact]
    public async Task GetRegistrationBreakdown_NoTouristProfile_ReturnsUnauthorized()
    {
        var ctx = BuildContext();
        var svc = BuildService(ctx);

        // User with no tourist profile
        var result = await svc.GetRegistrationBreakdownAsync(registrationId: 1, requestingUserId: 777);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.Unauthorized, result.Error);
    }

    // ── EstimateOrganizedTripCostAsync ────────────────────────────────────────

    [Fact]
    public async Task EstimateOrganizedTripCost_NormalCase_ReturnsCorrectEstimate()
    {
        var ctx = BuildContext();
        var (_, _, trip, _) = await SeedRegistrationAsync(ctx, pricePerPerson: 300m);
        var svc = BuildService(ctx);

        var result = await svc.EstimateOrganizedTripCostAsync(trip.Id, numberOfTravelers: 5);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(300m, result.Data.BasePrice);
        Assert.Equal(300m, result.Data.PricePerPerson);
        Assert.Equal(1500m, result.Data.TotalAmount);   // 300 × 5
        Assert.Equal(5, result.Data.NumberOfTravelers);
        Assert.Null(result.Data.RegistrationId);         // no registration yet
    }

    [Fact]
    public async Task EstimateOrganizedTripCost_OneTraveler_TotalEqualsBasePrice()
    {
        var ctx = BuildContext();
        var (_, _, trip, _) = await SeedRegistrationAsync(ctx, pricePerPerson: 450m);
        var svc = BuildService(ctx);

        var result = await svc.EstimateOrganizedTripCostAsync(trip.Id, numberOfTravelers: 1);

        Assert.True(result.Success);
        Assert.Equal(450m, result.Data!.TotalAmount);
    }

    [Fact]
    public async Task EstimateOrganizedTripCost_TripNotFound_ReturnsNotFound()
    {
        var ctx = BuildContext();
        var svc = BuildService(ctx);

        var result = await svc.EstimateOrganizedTripCostAsync(organizedTripId: 9999, numberOfTravelers: 2);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.OrganizedTripNotFound, result.Error);
    }

    [Fact]
    public async Task EstimateOrganizedTripCost_ZeroTravelers_ReturnsInvalidRequest()
    {
        var ctx = BuildContext();
        var (_, _, trip, _) = await SeedRegistrationAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.EstimateOrganizedTripCostAsync(trip.Id, numberOfTravelers: 0);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.InvalidRequest, result.Error);
    }

    [Fact]
    public async Task EstimateOrganizedTripCost_NegativeTravelers_ReturnsInvalidRequest()
    {
        var ctx = BuildContext();
        var (_, _, trip, _) = await SeedRegistrationAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.EstimateOrganizedTripCostAsync(trip.Id, numberOfTravelers: -3);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.InvalidRequest, result.Error);
    }

    [Fact]
    public async Task EstimateOrganizedTripCost_InvalidTripId_ReturnsInvalidRequest()
    {
        var ctx = BuildContext();
        var svc = BuildService(ctx);

        var result = await svc.EstimateOrganizedTripCostAsync(organizedTripId: 0, numberOfTravelers: 2);

        Assert.False(result.Success);
        Assert.Equal(CostBreakdownError.InvalidRequest, result.Error);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CostBreakdownController — HTTP status mapping tests
    // ═══════════════════════════════════════════════════════════════════════════

    private static CostBreakdownController BuildController(ITripCostService svc, int userId)
    {
        var controller = new CostBreakdownController(svc);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "Tourist")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "TestAuth"), null)
            }
        };
        return controller;
    }

    [Fact]
    public async Task Controller_GetProposalBreakdown_ReturnsOkOnSuccess()
    {
        var ctx = BuildContext();
        var (tourist, _, _, proposal) = await SeedProposalAsync(ctx);
        var controller = BuildController(BuildService(ctx), tourist.Id);

        var action = await controller.GetProposalBreakdown(proposal.Id);

        var ok = Assert.IsType<OkObjectResult>(action);
        var result = Assert.IsType<CostBreakdownResultDto>(ok.Value);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Controller_GetProposalBreakdown_UnauthorizedUser_Returns403()
    {
        var ctx = BuildContext();
        var (_, _, _, proposal) = await SeedProposalAsync(ctx);
        var controller = BuildController(BuildService(ctx), userId: 999);

        var action = await controller.GetProposalBreakdown(proposal.Id);

        var obj = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    [Fact]
    public async Task Controller_GetProposalBreakdown_NotFound_Returns404()
    {
        var ctx = BuildContext();
        var (tourist, _, _, _) = await SeedProposalAsync(ctx);
        var controller = BuildController(BuildService(ctx), tourist.Id);

        var action = await controller.GetProposalBreakdown(proposalId: 9999);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public async Task Controller_GetRegistrationBreakdown_ReturnsOkOnSuccess()
    {
        var ctx = BuildContext();
        var (tourist, _, _, registration) = await SeedRegistrationAsync(ctx);
        var controller = BuildController(BuildService(ctx), tourist.Id);

        var action = await controller.GetRegistrationBreakdown(registration.Id);

        var ok = Assert.IsType<OkObjectResult>(action);
        var result = Assert.IsType<CostBreakdownResultDto>(ok.Value);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Controller_GetRegistrationBreakdown_UnauthorizedUser_Returns403()
    {
        var ctx = BuildContext();
        var (_, _, _, registration) = await SeedRegistrationAsync(ctx);
        // Seed a separate tourist with their own profile so the service passes the profile check
        var otherUser = new User
        {
            Id = 77, FullName = "Uninvited", Email = "u@u.com",
            PhoneNumber = "01777000077", Role = UserRole.Tourist, IsActive = true
        };
        ctx.Users.Add(otherUser);
        ctx.TouristProfiles.Add(new TouristProfile { Id = 770, UserId = otherUser.Id });
        await ctx.SaveChangesAsync();

        var controller = BuildController(BuildService(ctx), otherUser.Id);

        var action = await controller.GetRegistrationBreakdown(registration.Id);

        var obj = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    [Fact]
    public async Task Controller_EstimateOrganizedTripCost_ReturnsOkOnSuccess()
    {
        var ctx = BuildContext();
        var (tourist, _, trip, _) = await SeedRegistrationAsync(ctx, pricePerPerson: 100m);
        var controller = BuildController(BuildService(ctx), tourist.Id);

        var action = await controller.EstimateOrganizedTripCost(trip.Id, travelers: 3);

        var ok = Assert.IsType<OkObjectResult>(action);
        var result = Assert.IsType<CostBreakdownResultDto>(ok.Value);
        Assert.True(result.Success);
        Assert.Equal(300m, result.Data!.TotalAmount);
    }

    [Fact]
    public async Task Controller_EstimateOrganizedTripCost_ZeroTravelers_Returns400()
    {
        var ctx = BuildContext();
        var (tourist, _, trip, _) = await SeedRegistrationAsync(ctx);
        var controller = BuildController(BuildService(ctx), tourist.Id);

        // The controller validates travelers < 1 before calling the service.
        var action = await controller.EstimateOrganizedTripCost(trip.Id, travelers: 0);

        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Fact]
    public async Task Controller_EstimateOrganizedTripCost_TripNotFound_Returns404()
    {
        var ctx = BuildContext();
        var (tourist, _, _, _) = await SeedRegistrationAsync(ctx);
        var controller = BuildController(BuildService(ctx), tourist.Id);

        var action = await controller.EstimateOrganizedTripCost(organizedTripId: 9999, travelers: 2);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public async Task Controller_GetProposalBreakdown_MissingAuthClaim_Returns401()
    {
        var ctx = BuildContext();
        var (_, _, _, proposal) = await SeedProposalAsync(ctx);

        // Controller with no NameIdentifier claim
        var controller = new CostBreakdownController(BuildService(ctx));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(), null)
            }
        };

        var action = await controller.GetProposalBreakdown(proposal.Id);

        Assert.IsType<UnauthorizedObjectResult>(action);
    }
}
