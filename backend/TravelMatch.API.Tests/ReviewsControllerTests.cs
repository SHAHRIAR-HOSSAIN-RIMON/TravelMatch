using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Reviews;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Controller-level tests for ReviewsController using the real ReviewService over
/// EF Core InMemory, following the direct-instantiation pattern used by
/// ProposalsController/TripMessagesController tests. Framework [Authorize] attributes
/// are not executed here — these tests cover claim extraction and HTTP mappings.
/// </summary>
public class ReviewsControllerTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static ReviewService BuildService(ApplicationDbContext ctx) =>
        new(ctx, NullLogger<ReviewService>.Instance);

    private static ReviewsController BuildController(
        ReviewService service, int? userId = 2, string? role = "Tourist")
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        return new ReviewsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static async Task<GuideProfile> SeedGuideAsync(
        ApplicationDbContext ctx, int userId = 10, int profileId = 100, string fullName = "Guide One")
    {
        ctx.Users.Add(new User
        {
            Id = userId, FullName = fullName, Email = $"guide{userId}@t.com",
            PhoneNumber = $"0170{userId:000000}", Role = UserRole.Guide, IsActive = true
        });
        var profile = new GuideProfile
        {
            Id = profileId, UserId = userId, Bio = "Experienced guide", ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure", ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };
        ctx.GuideProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return profile;
    }

    /// <summary>
    /// Seeds tourist 2 with a completed trip served by guide 10 (profile 100).
    /// </summary>
    private static async Task<GuideProfile> SeedCompletedScenarioAsync(ApplicationDbContext ctx)
    {
        ctx.Users.Add(new User
        {
            Id = 2, FullName = "Tourist Two", Email = "tourist2@t.com",
            PhoneNumber = "01700000002", Role = UserRole.Tourist, IsActive = true
        });
        var guide = await SeedGuideAsync(ctx);

        ctx.TripRequests.Add(new TripRequest
        {
            Id = 300, TouristId = 2, Destination = "Cox's Bazar", TripType = "Beach",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            NumberOfTravelers = 2, Budget = 600m, BudgetMin = 400m, BudgetMax = 700m,
            Description = "Test trip", TravelPreferences = "Relaxed",
            Status = TripRequestStatus.Completed, CompletedAt = DateTime.UtcNow.AddDays(-1),
            MatchedGuideId = guide.UserId
        });
        await ctx.SaveChangesAsync();
        return guide;
    }

    private static CreateReviewRequestDto Request(int guideProfileId, int rating = 5) =>
        new() { TripRequestId = 300, GuideId = guideProfileId, Rating = rating, Comment = "Great trip" };

    // ═════════════════════════════════════════════════════════════════════════
    // POST /api/reviews
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SubmitReview_ValidRequest_Returns201AndPersistsReview()
    {
        await using var ctx = BuildContext(nameof(SubmitReview_ValidRequest_Returns201AndPersistsReview));
        var guide = await SeedCompletedScenarioAsync(ctx);
        var controller = BuildController(BuildService(ctx));

        var actionResult = await controller.SubmitReview(Request(guide.Id));

        var created = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var body = Assert.IsType<ReviewResultDto>(created.Value);
        Assert.True(body.Success);
        Assert.Equal(ReviewError.None, body.Error);
        Assert.NotNull(body.Data);

        var saved = await ctx.Reviews.SingleAsync();
        Assert.Equal(2, saved.TouristId);
        Assert.Equal(guide.Id, saved.GuideId);
        Assert.Equal(300, saved.TripRequestId);
        Assert.Equal(5, saved.Rating);

        var profile = await ctx.GuideProfiles.AsNoTracking().SingleAsync(gp => gp.Id == guide.Id);
        Assert.Equal(5m, profile.AverageRating);
    }

    [Fact]
    public async Task SubmitReview_MissingNameIdentifierClaim_Returns401()
    {
        await using var ctx = BuildContext(nameof(SubmitReview_MissingNameIdentifierClaim_Returns401));
        var guide = await SeedCompletedScenarioAsync(ctx);
        // Authenticated identity without a NameIdentifier claim.
        var controller = BuildController(BuildService(ctx), userId: null);

        var actionResult = await controller.SubmitReview(Request(guide.Id));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task SubmitReview_DuplicateReview_Returns409()
    {
        await using var ctx = BuildContext(nameof(SubmitReview_DuplicateReview_Returns409));
        var guide = await SeedCompletedScenarioAsync(ctx);
        var controller = BuildController(BuildService(ctx));

        var first = await controller.SubmitReview(Request(guide.Id));
        var second = await controller.SubmitReview(Request(guide.Id));

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(first).StatusCode);

        var conflict = Assert.IsType<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var body = Assert.IsType<ReviewResultDto>(conflict.Value);
        Assert.Equal(ReviewError.DuplicateReview, body.Error);
        Assert.Single(ctx.Reviews);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GET /api/reviews/guide/{guideProfileId}
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetGuideReviews_UnknownGuide_Returns404()
    {
        await using var ctx = BuildContext(nameof(GetGuideReviews_UnknownGuide_Returns404));
        var controller = BuildController(BuildService(ctx));

        var actionResult = await controller.GetGuideReviews(guideProfileId: 999);

        var notFound = Assert.IsType<NotFoundObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        var body = Assert.IsType<ReviewListResultDto>(notFound.Value);
        Assert.Equal(ReviewError.GuideNotFound, body.Error);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetGuideReviews_InvalidPagination_Returns400(int page, int pageSize)
    {
        await using var ctx = BuildContext($"{nameof(GetGuideReviews_InvalidPagination_Returns400)}_{page}_{pageSize}");
        var controller = BuildController(BuildService(ctx));

        var actionResult = await controller.GetGuideReviews(guideProfileId: 100, page, pageSize);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GET /api/reviews/guide/{guideProfileId}/rating
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetGuideRating_GuideWithoutReviews_Returns200WithNullAverage()
    {
        await using var ctx = BuildContext(nameof(GetGuideRating_GuideWithoutReviews_Returns200WithNullAverage));
        var guide = await SeedGuideAsync(ctx);
        var controller = BuildController(BuildService(ctx));

        var actionResult = await controller.GetGuideRating(guide.Id);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var body = Assert.IsType<GuideRatingDto>(ok.Value);
        Assert.Equal(guide.Id, body.GuideId);
        Assert.Null(body.AverageRating);
        Assert.Equal(0, body.ReviewCount);
    }
}
