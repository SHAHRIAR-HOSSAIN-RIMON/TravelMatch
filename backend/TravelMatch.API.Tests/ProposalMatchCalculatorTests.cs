using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Proposals;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

public class ProposalMatchCalculatorTests
{
    [Fact]
    public void BudgetCompatibility_WithinBudgetScoresHigherThanOverBudget()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Dhaka",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            Status = TripRequestStatus.Pending,
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            TripType = "Adventure"
        };

        var guideProfile = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure",
            ExperienceYears = 6,
            AverageRating = 4.5m,
            VerificationStatus = VerificationStatus.Verified
        };

        var withinBudgetProposal = new Proposal
        {
            Id = 1,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Available during 2026-11-10 to 2026-11-15",
            Status = ProposalStatus.Submitted
        };

        var overBudgetProposal = new Proposal
        {
            Id = 2,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 32000m,
            Availability = "Available during 2026-11-10 to 2026-11-15",
            Status = ProposalStatus.Submitted
        };

        var withinBudget = ProposalMatchCalculator.Calculate(tripRequest, withinBudgetProposal, guideProfile);
        var overBudget = ProposalMatchCalculator.Calculate(tripRequest, overBudgetProposal, guideProfile);

        Assert.True(withinBudget.TotalScore > overBudget.TotalScore);
        Assert.InRange(withinBudget.TotalScore, 0m, 100m);
        Assert.InRange(overBudget.TotalScore, 0m, 100m);
    }

    [Fact]
    public void DestinationCompatibility_ChangesTheScore()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Dhaka",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            Status = TripRequestStatus.Pending,
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            TripType = "Adventure"
        };

        var matchingGuide = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure",
            ExperienceYears = 5,
            AverageRating = 4.6m,
            VerificationStatus = VerificationStatus.Verified
        };

        var mismatchedGuide = new GuideProfile
        {
            UserId = 8,
            ServiceArea = "Sylhet",
            SupportedTripTypes = "Adventure",
            ExperienceYears = 5,
            AverageRating = 4.6m,
            VerificationStatus = VerificationStatus.Verified
        };

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 17000m,
            Availability = "Available on 2026-11-10",
            Status = ProposalStatus.Submitted
        };

        var matchingResult = ProposalMatchCalculator.Calculate(tripRequest, proposal, matchingGuide);
        var mismatchedResult = ProposalMatchCalculator.Calculate(tripRequest, proposal, mismatchedGuide);

        Assert.True(matchingResult.TotalScore > mismatchedResult.TotalScore);
    }

    [Fact]
    public void ExperienceAndRating_AreIncluded_WhenDataExists()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Dhaka",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            Status = TripRequestStatus.Pending,
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            TripType = "Adventure"
        };

        var guideProfile = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure",
            ExperienceYears = 10,
            AverageRating = 5m,
            VerificationStatus = VerificationStatus.Verified
        };

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Available for 2026-11-01 to 2026-11-08",
            Status = ProposalStatus.Submitted
        };

        var result = ProposalMatchCalculator.Calculate(tripRequest, proposal, guideProfile);

        Assert.True(result.ExperienceScore > 0m);
        Assert.True(result.TotalScore >= 70m);
    }

    [Fact]
    public void MissingOptionalData_DoesNotCrash_AndDoesNotInventPerfectScores()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Dhaka",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            Status = TripRequestStatus.Pending,
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            TripType = "Adventure"
        };

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 19000m,
            Availability = string.Empty,
            Status = ProposalStatus.Submitted
        };

        var result = ProposalMatchCalculator.Calculate(tripRequest, proposal, new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure",
            VerificationStatus = VerificationStatus.Verified
        });

        Assert.InRange(result.TotalScore, 0m, 100m);
        Assert.Equal(0m, result.AvailabilityScore);
        Assert.True(result.TotalScore < 100m);
    }

    [Fact]
    public void BestMatchSort_RanksDescending_AndTiesAreStable()
    {
        var lower = new ProposalMatchResult { ProposalId = 10, TotalScore = 72m };
        var first = new ProposalMatchResult { ProposalId = 20, TotalScore = 90m };
        var second = new ProposalMatchResult { ProposalId = 30, TotalScore = 90m };

        var ordered = new[] { lower, first, second }
            .OrderByDescending(x => x.TotalScore)
            .ThenBy(x => x.ProposalId)
            .ToList();

        Assert.Equal(new[] { 20, 30, 10 }, ordered.Select(x => x.ProposalId));
    }

    [Fact]
    public void TripTypeMatch_IsCaseInsensitive_AndMismatchReturnsZero()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Dhaka",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            TripType = "Adventure Travel",
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            Status = TripRequestStatus.Pending
        };

        var matchingGuide = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "ADVENTURE, cultural",
            AverageRating = 4.2m,
            ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };

        var mismatchedGuide = new GuideProfile
        {
            UserId = 8,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Cultural",
            AverageRating = 4.2m,
            ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Available on 2026-11-10",
            Status = ProposalStatus.Submitted
        };

        var match = ProposalMatchCalculator.Calculate(tripRequest, proposal, matchingGuide);
        var mismatch = ProposalMatchCalculator.Calculate(tripRequest, proposal, mismatchedGuide);

        Assert.Equal((decimal)ProposalMatchCalculator.TripTypeWeight, match.TripTypeScore);
        Assert.Equal(0m, mismatch.TripTypeScore);
    }

    [Fact]
    public void DestinationNormalization_HandlesWhitespacePunctuationAndCase()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "   new\t  york ",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            TripType = "Adventure",
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            Status = TripRequestStatus.Pending
        };

        var guideProfile = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "NEW-YORK",
            SupportedTripTypes = "Adventure",
            AverageRating = 4.2m,
            ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Available on 2026-11-10",
            Status = ProposalStatus.Submitted
        };

        var result = ProposalMatchCalculator.Calculate(tripRequest, proposal, guideProfile);

        Assert.Equal((decimal)ProposalMatchCalculator.DestinationWeight, result.DestinationScore);
    }

    [Fact]
    public void DestinationNormalization_DoesNotMatchUnrelatedOrPartialLocations()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Paris",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            TripType = "Adventure",
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            Status = TripRequestStatus.Pending
        };

        var guideProfile = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Paris Central",
            SupportedTripTypes = "Adventure",
            AverageRating = 4.2m,
            ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };

        var mismatchedGuide = new GuideProfile
        {
            UserId = 8,
            ServiceArea = "Portland",
            SupportedTripTypes = "Adventure",
            AverageRating = 4.2m,
            ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Available on 2026-11-10",
            Status = ProposalStatus.Submitted
        };

        var matching = ProposalMatchCalculator.Calculate(tripRequest, proposal, guideProfile);
        var mismatched = ProposalMatchCalculator.Calculate(tripRequest, proposal, mismatchedGuide);

        Assert.Equal((decimal)ProposalMatchCalculator.DestinationWeight, matching.DestinationScore);
        Assert.Equal(0m, mismatched.DestinationScore);
    }

    [Fact]
    public void AvailabilityScore_AcceptsSupportedFormats_AndRejectsNegationOrAmbiguity()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Dhaka",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            TripType = "Adventure",
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            Status = TripRequestStatus.Pending
        };

        var guideProfile = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure",
            AverageRating = 4.2m,
            ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };

        foreach (var availability in new[]
        {
            "Available on 2026-11-10",
            "Available on 10 Nov 2026",
            "Available on 11/10/2026",
            "Available on 10-11-2026",
            "Available on 2026/11/10"
        })
        {
            var proposal = new Proposal
            {
                Id = 1,
                GuideId = 7,
                TripRequestId = 1,
                ProposedPrice = 18000m,
                Availability = availability,
                Status = ProposalStatus.Submitted
            };

            var result = ProposalMatchCalculator.Calculate(tripRequest, proposal, guideProfile);
            Assert.Equal((decimal)ProposalMatchCalculator.AvailabilityWeight, result.AvailabilityScore);
        }

        var negated = new Proposal
        {
            Id = 2,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Not available on 2026-11-12",
            Status = ProposalStatus.Submitted
        };

        var ambiguous = new Proposal
        {
            Id = 3,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Flexible around November 2026",
            Status = ProposalStatus.Submitted
        };

        Assert.Equal(0m, ProposalMatchCalculator.Calculate(tripRequest, negated, guideProfile).AvailabilityScore);
        Assert.Equal(0m, ProposalMatchCalculator.Calculate(tripRequest, ambiguous, guideProfile).AvailabilityScore);
    }

    [Fact]
    public void RatingAndExperience_AreClampedWithinBounds()
    {
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Dhaka",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            TripType = "Adventure",
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            Status = TripRequestStatus.Pending
        };

        var guideProfile = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure",
            AverageRating = 9m,
            ExperienceYears = 20,
            VerificationStatus = VerificationStatus.Verified
        };

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 7,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Available on 2026-11-10",
            Status = ProposalStatus.Submitted
        };

        var result = ProposalMatchCalculator.Calculate(tripRequest, proposal, guideProfile);

        Assert.InRange(result.ExperienceScore, 0m, (decimal)ProposalMatchCalculator.ExperienceWeight);
        Assert.InRange(result.TotalScore, 0m, 100m);
    }

    [Fact]
    public async Task GetProposalsAsync_RejectsUnauthorizedAccess_ForOtherTourist()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);

        context.Users.Add(new User { Id = 1, FullName = "Tourist 1", Email = "tourist1@test.com", PhoneNumber = "111", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
        context.Users.Add(new User { Id = 2, FullName = "Tourist 2", Email = "tourist2@test.com", PhoneNumber = "222", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
        context.Users.Add(new User { Id = 7, FullName = "Guide", Email = "guide@test.com", PhoneNumber = "333", PasswordHash = "hash", Role = UserRole.Guide, IsActive = true });
        context.GuideProfiles.Add(new GuideProfile { Id = 1, UserId = 7, ServiceArea = "Dhaka", SupportedTripTypes = "Adventure", ExperienceYears = 4, AverageRating = 4.3m, VerificationStatus = VerificationStatus.Verified });
        context.TripRequests.Add(new TripRequest { Id = 5, TouristId = 2, Destination = "Dhaka", TripType = "Adventure", StartDate = new DateOnly(2026, 11, 10), EndDate = new DateOnly(2026, 11, 20), Budget = 20000m, BudgetMin = 15000m, BudgetMax = 25000m, Status = TripRequestStatus.Pending });
        context.Proposals.Add(new Proposal { Id = 1, TripRequestId = 5, GuideId = 7, ProposedPrice = 18000m, Availability = "Available on 2026-11-10", Status = ProposalStatus.Submitted });
        await context.SaveChangesAsync();

        var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());

        var result = await service.GetProposalsAsync(1, 5, ProposalSort.BestMatch);

        Assert.False(result.Success);
        Assert.Equal(ProposalError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task GetProposalsAsync_EmptyResult_WhenNoEligibleProposalsExist()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Users.Add(new User { Id = 1, FullName = "Tourist 1", Email = "tourist1@test.com", PhoneNumber = "111", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
        context.Users.Add(new User { Id = 7, FullName = "Guide", Email = "guide@test.com", PhoneNumber = "333", PasswordHash = "hash", Role = UserRole.Guide, IsActive = true });
        context.GuideProfiles.Add(new GuideProfile { Id = 1, UserId = 7, ServiceArea = "Dhaka", SupportedTripTypes = "Adventure", ExperienceYears = 4, AverageRating = 4.3m, VerificationStatus = VerificationStatus.Verified });
        context.TripRequests.Add(new TripRequest { Id = 5, TouristId = 1, Destination = "Dhaka", TripType = "Adventure", StartDate = new DateOnly(2026, 11, 10), EndDate = new DateOnly(2026, 11, 20), Budget = 20000m, BudgetMin = 15000m, BudgetMax = 25000m, Status = TripRequestStatus.Pending });
        context.Proposals.Add(new Proposal { Id = 1, TripRequestId = 5, GuideId = 7, ProposedPrice = 18000m, Availability = "Available on 2026-11-10", Status = ProposalStatus.Draft });
        await context.SaveChangesAsync();

        var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());

        var result = await service.GetProposalsAsync(1, 5, ProposalSort.BestMatch);

        Assert.True(result.Success);
        Assert.Equal(ProposalError.None, result.Error);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Data ?? Array.Empty<ProposalDto>());
    }

    [Fact]
    public async Task GetProposalsAsync_RejectsMissingTripRequestId()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());

        var result = await service.GetProposalsAsync(1, 0, ProposalSort.BestMatch);

        Assert.False(result.Success);
        Assert.Equal(ProposalError.TripRequestNotFound, result.Error);
    }

    [Fact]
    public async Task GetProposalsAsync_ExcludesWithdrawnAndRejectedProposalsFromReviewList()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Users.Add(new User { Id = 1, FullName = "Tourist 1", Email = "tourist1@test.com", PhoneNumber = "111", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
        context.Users.Add(new User { Id = 7, FullName = "Guide", Email = "guide@test.com", PhoneNumber = "333", PasswordHash = "hash", Role = UserRole.Guide, IsActive = true });
        context.GuideProfiles.Add(new GuideProfile { Id = 1, UserId = 7, ServiceArea = "Dhaka", SupportedTripTypes = "Adventure", ExperienceYears = 4, AverageRating = 4.3m, VerificationStatus = VerificationStatus.Verified });
        context.TripRequests.Add(new TripRequest { Id = 5, TouristId = 1, Destination = "Dhaka", TripType = "Adventure", StartDate = new DateOnly(2026, 11, 10), EndDate = new DateOnly(2026, 11, 20), Budget = 20000m, BudgetMin = 15000m, BudgetMax = 25000m, Status = TripRequestStatus.Pending });
        context.Proposals.Add(new Proposal { Id = 1, TripRequestId = 5, GuideId = 7, ProposedPrice = 18000m, Availability = "Available on 2026-11-10", Status = ProposalStatus.Submitted });
        context.Proposals.Add(new Proposal { Id = 2, TripRequestId = 5, GuideId = 7, ProposedPrice = 19000m, Availability = "Available on 2026-11-10", Status = ProposalStatus.Withdrawn });
        context.Proposals.Add(new Proposal { Id = 3, TripRequestId = 5, GuideId = 7, ProposedPrice = 20000m, Availability = "Available on 2026-11-10", Status = ProposalStatus.Rejected });
        await context.SaveChangesAsync();

        var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());
        var result = await service.GetProposalsAsync(1, 5, ProposalSort.BestMatch);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Data!, proposal => Assert.Equal(1, proposal.Id));
    }

    [Fact]
    public async Task GetProposalsAsync_DoesNotPersistCalculatedScoresToTheDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("proposal-read-no-write")
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            context.Users.Add(new User { Id = 1, FullName = "Tourist 1", Email = "tourist1@test.com", PhoneNumber = "111", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
            context.Users.Add(new User { Id = 7, FullName = "Guide", Email = "guide@test.com", PhoneNumber = "333", PasswordHash = "hash", Role = UserRole.Guide, IsActive = true });
            context.GuideProfiles.Add(new GuideProfile { Id = 1, UserId = 7, ServiceArea = "Dhaka", SupportedTripTypes = "Adventure", ExperienceYears = 4, AverageRating = 4.3m, VerificationStatus = VerificationStatus.Verified });
            context.TripRequests.Add(new TripRequest { Id = 5, TouristId = 1, Destination = "Dhaka", TripType = "Adventure", StartDate = new DateOnly(2026, 11, 10), EndDate = new DateOnly(2026, 11, 20), Budget = 20000m, BudgetMin = 15000m, BudgetMax = 25000m, Status = TripRequestStatus.Pending });
            context.Proposals.Add(new Proposal { Id = 1, TripRequestId = 5, GuideId = 7, ProposedPrice = 18000m, Availability = "Available on 2026-11-10", Status = ProposalStatus.Submitted });
            await context.SaveChangesAsync();
        }

        await using (var context = new ApplicationDbContext(options))
        {
            var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());
            var result = await service.GetProposalsAsync(1, 5, ProposalSort.BestMatch);

            Assert.True(result.Success);
            var stored = await context.Proposals.AsNoTracking().SingleAsync();
            Assert.Null(stored.MatchingScore);
        }
    }

    [Fact]
    public async Task AcceptProposalAsync_AcceptsOnlySubmittedProposals()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Users.Add(new User { Id = 1, FullName = "Tourist 1", Email = "tourist1@test.com", PhoneNumber = "111", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
        context.Users.Add(new User { Id = 7, FullName = "Guide", Email = "guide@test.com", PhoneNumber = "333", PasswordHash = "hash", Role = UserRole.Guide, IsActive = true });
        context.GuideProfiles.Add(new GuideProfile { Id = 1, UserId = 7, ServiceArea = "Dhaka", SupportedTripTypes = "Adventure", ExperienceYears = 4, AverageRating = 4.3m, VerificationStatus = VerificationStatus.Verified });
        context.TripRequests.Add(new TripRequest { Id = 5, TouristId = 1, Destination = "Dhaka", TripType = "Adventure", StartDate = new DateOnly(2026, 11, 10), EndDate = new DateOnly(2026, 11, 20), Budget = 20000m, BudgetMin = 15000m, BudgetMax = 25000m, Status = TripRequestStatus.Pending });
        context.Proposals.Add(new Proposal { Id = 1, TripRequestId = 5, GuideId = 7, ProposedPrice = 18000m, Availability = "Available on 2026-11-10", Status = ProposalStatus.Submitted });
        await context.SaveChangesAsync();

        var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());
        var accepted = await service.AcceptProposalAsync(1, 5, 1);

        Assert.True(accepted.Success);
        Assert.Equal(ProposalStatus.Accepted, (await context.Proposals.SingleAsync(p => p.Id == 1)).Status);
    }

    [Fact]
    public async Task ProposalsController_UsesUnauthorizedHttpStatus_WhenTokenIsMissing()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        var controller = new ProposalsController(new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        var actionResult = await controller.GetProposals(1, ProposalSort.BestMatch);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task ProposalsController_Returns200Ok_WhenNoProposalsExist()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Users.Add(new User { Id = 1, FullName = "Tourist 1", Email = "tourist1@test.com", PhoneNumber = "111", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
        context.TripRequests.Add(new TripRequest { Id = 5, TouristId = 1, Destination = "Dhaka", TripType = "Adventure", StartDate = new DateOnly(2026, 11, 10), EndDate = new DateOnly(2026, 11, 20), Budget = 20000m, BudgetMin = 15000m, BudgetMax = 25000m, Status = TripRequestStatus.Pending });
        await context.SaveChangesAsync();

        var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());
        var controller = new ProposalsController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, "Tourist")
                }, "TestAuth"))
            }
        };

        var actionResult = await controller.GetProposals(5, ProposalSort.BestMatch);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var body = Assert.IsType<ProposalListResultDto>(okResult.Value);
        Assert.True(body.Success);
        Assert.Equal(ProposalError.None, body.Error);
        Assert.Equal(0, body.TotalCount);
    }

    [Fact]
    public async Task AcceptProposalAsync_UsesEnumError_WhenTripRequestNotFound()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());

        var result = await service.AcceptProposalAsync(1, 999, 1);

        Assert.False(result.Success);
        Assert.Equal(AcceptProposalError.TripRequestNotFound, result.Error);
        Assert.IsType<AcceptProposalError>(result.Error);
    }

    [Fact]
    public async Task ProposalsController_Returns403Forbidden_WhenUnauthorized()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Users.Add(new User { Id = 1, FullName = "Tourist 1", Email = "tourist1@test.com", PhoneNumber = "111", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
        context.Users.Add(new User { Id = 2, FullName = "Tourist 2", Email = "tourist2@test.com", PhoneNumber = "222", PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true });
        context.TripRequests.Add(new TripRequest { Id = 5, TouristId = 2, Destination = "Dhaka", TripType = "Adventure", StartDate = new DateOnly(2026, 11, 10), EndDate = new DateOnly(2026, 11, 20), Budget = 20000m, BudgetMin = 15000m, BudgetMax = 25000m, Status = TripRequestStatus.Pending });
        await context.SaveChangesAsync();

        var service = new ProposalService(context, new LoggerFactory().CreateLogger<ProposalService>());
        var controller = new ProposalsController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, "Tourist")
                }, "TestAuth"))
            }
        };

        var actionResult = await controller.GetProposals(5, ProposalSort.BestMatch);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void DateFormatDisambiguation_SlashIsMmDdYyyy_DashIsDdMmYyyy()
    {
        // "11/10/2026" with slash delimiter → MM/dd/yyyy → November 10
        var tripRequest = new TripRequest
        {
            Id = 1,
            Destination = "Dhaka",
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            TripType = "Adventure",
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 15),
            Status = TripRequestStatus.Pending
        };

        var guideProfile = new GuideProfile
        {
            UserId = 7,
            ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure",
            AverageRating = 4.2m,
            ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };

        // Slash: "11/10/2026" parses as MM/dd/yyyy = Nov 10 → within trip range
        var slashProposal = new Proposal
        {
            Id = 1, GuideId = 7, TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Available on 11/10/2026",
            Status = ProposalStatus.Submitted
        };

        var slashResult = ProposalMatchCalculator.Calculate(tripRequest, slashProposal, guideProfile);
        Assert.Equal((decimal)ProposalMatchCalculator.AvailabilityWeight, slashResult.AvailabilityScore);

        // Dash: "10-11-2026" parses as dd-MM-yyyy = Nov 10 → within trip range
        var dashProposal = new Proposal
        {
            Id = 2, GuideId = 7, TripRequestId = 1,
            ProposedPrice = 18000m,
            Availability = "Available on 10-11-2026",
            Status = ProposalStatus.Submitted
        };

        var dashResult = ProposalMatchCalculator.Calculate(tripRequest, dashProposal, guideProfile);
        Assert.Equal((decimal)ProposalMatchCalculator.AvailabilityWeight, dashResult.AvailabilityScore);
    }
}
