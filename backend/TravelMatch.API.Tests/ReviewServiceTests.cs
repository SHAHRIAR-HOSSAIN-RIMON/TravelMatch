using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Reviews;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for ReviewService against EF Core InMemory:
///   - Submission eligibility: completed trip, ownership, guide match, self-review
///   - Rating bounds, duplicate reviews, and the unique-index race fallback
///   - Average recalculation across persisted reviews
///   - Guide review listing: names, newest-first ordering, stable ties, pagination
///   - Guide rating lookup including the no-reviews (null average) case
/// </summary>
public class ReviewServiceTests
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

    private static DateOnly PastDate(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

    private static CreateReviewRequestDto Request(int tripId, int guideProfileId, int rating = 5, string? comment = null) =>
        new() { TripRequestId = tripId, GuideId = guideProfileId, Rating = rating, Comment = comment };

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static async Task SeedUserAsync(
        ApplicationDbContext ctx, int userId, string fullName, UserRole role)
    {
        if (await ctx.Users.FindAsync(userId) is null)
        {
            ctx.Users.Add(new User
            {
                Id = userId, FullName = fullName, Email = $"u{userId}@t.com",
                PhoneNumber = $"01700{userId:000000}", Role = role, IsActive = true
            });
            await ctx.SaveChangesAsync();
        }
    }

    private record GuideSeed(int UserId, int ProfileId, string FullName);

    private static async Task<GuideSeed> SeedGuideAsync(
        ApplicationDbContext ctx, int userId = 10, int profileId = 100, string fullName = "Guide One")
    {
        await SeedUserAsync(ctx, userId, fullName, UserRole.Guide);

        if (await ctx.GuideProfiles.FindAsync(profileId) is null)
        {
            ctx.GuideProfiles.Add(new GuideProfile
            {
                Id = profileId,
                UserId = userId,
                Bio = "Experienced guide",
                ServiceArea = "Dhaka",
                SupportedTripTypes = "Adventure",
                ExperienceYears = 5,
                VerificationStatus = VerificationStatus.Verified
            });
            await ctx.SaveChangesAsync();
        }

        return new GuideSeed(userId, profileId, fullName);
    }

    private static async Task SeedTripRequestAsync(
        ApplicationDbContext ctx,
        int tripId = 300,
        int touristUserId = 2,
        int? matchedGuideUserId = 10,
        TripRequestStatus status = TripRequestStatus.Completed,
        bool completedAtSet = true)
    {
        ctx.TripRequests.Add(new TripRequest
        {
            Id = tripId,
            TouristId = touristUserId,
            Destination = "Cox's Bazar",
            TripType = "Beach",
            StartDate = PastDate(10),
            EndDate = PastDate(5),
            NumberOfTravelers = 2,
            Budget = 600m,
            BudgetMin = 400m,
            BudgetMax = 700m,
            Description = "Test trip",
            TravelPreferences = "Relaxed",
            Status = status,
            CompletedAt = status == TripRequestStatus.Completed && completedAtSet
                ? DateTime.UtcNow.AddDays(-1)
                : null,
            MatchedGuideId = matchedGuideUserId
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the standard scenario: tourist 2 on completed trip 300 served by guide 10
    /// (profile 100).
    /// </summary>
    private static async Task<(GuideSeed Guide, int TripId)> SeedCompletedScenarioAsync(
        ApplicationDbContext ctx)
    {
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        var guide = await SeedGuideAsync(ctx);
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 2, matchedGuideUserId: guide.UserId);
        return (guide, 300);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Submission — happy path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Submit_ValidCompletedTrip_CreatesReviewAndResolvesNames()
    {
        await using var ctx = BuildContext(nameof(Submit_ValidCompletedTrip_CreatesReviewAndResolvesNames));
        var (guide, tripId) = await SeedCompletedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(
            touristUserId: 2, Request(tripId, guide.ProfileId, rating: 5, comment: "  Excellent trip  "));

        Assert.True(result.Success);
        Assert.Equal(ReviewError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.Id > 0);
        Assert.Equal(2, result.Data.TouristId);
        Assert.Equal("Tourist Two", result.Data.TouristName);
        // GuideName resolves GuideProfile.UserId → User.FullName, not the profile id.
        Assert.Equal(guide.ProfileId, result.Data.GuideId);
        Assert.Equal("Guide One", result.Data.GuideName);
        Assert.Equal(tripId, result.Data.TripRequestId);
        Assert.Equal(5, result.Data.Rating);
        Assert.Equal("Excellent trip", result.Data.Comment);

        var saved = await ctx.Reviews.SingleAsync();
        Assert.Equal(guide.ProfileId, saved.GuideId);
        Assert.Equal(2, saved.TouristId);
        Assert.Equal(tripId, saved.TripRequestId);
    }

    [Fact]
    public async Task Submit_FirstReview_SetsGuideAverageRating()
    {
        await using var ctx = BuildContext(nameof(Submit_FirstReview_SetsGuideAverageRating));
        var (guide, tripId) = await SeedCompletedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(2, Request(tripId, guide.ProfileId, rating: 4));

        Assert.True(result.Success);
        var profile = await ctx.GuideProfiles.AsNoTracking().SingleAsync(gp => gp.Id == guide.ProfileId);
        Assert.Equal(4m, profile.AverageRating);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Submission — rating validation
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public async Task Submit_RatingOutOfBounds_ReturnsInvalidRating(int rating)
    {
        await using var ctx = BuildContext($"{nameof(Submit_RatingOutOfBounds_ReturnsInvalidRating)}_{rating}");
        var (guide, tripId) = await SeedCompletedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        // Bypasses API model validation — the service must validate independently.
        var result = await svc.SubmitReviewAsync(2, Request(tripId, guide.ProfileId, rating));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.InvalidRating, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Submission — caller eligibility
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Submit_NonexistentTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Submit_NonexistentTourist_ReturnsUnauthorized));
        var (guide, tripId) = await SeedCompletedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(touristUserId: 9999, Request(tripId, guide.ProfileId));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.Unauthorized, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task Submit_NonTouristUser_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Submit_NonTouristUser_ReturnsUnauthorized));
        var (guide, tripId) = await SeedCompletedScenarioAsync(ctx);
        // Guide-role user tries to act as a tourist.
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(touristUserId: guide.UserId, Request(tripId, guide.ProfileId));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.Unauthorized, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task Submit_TripOwnedByAnotherTourist_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(Submit_TripOwnedByAnotherTourist_ReturnsUnauthorized));
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        await SeedUserAsync(ctx, 3, "Tourist Three", UserRole.Tourist);
        var guide = await SeedGuideAsync(ctx);
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 3, matchedGuideUserId: guide.UserId);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(touristUserId: 2, Request(300, guide.ProfileId));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.Unauthorized, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Submission — trip eligibility
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Submit_NonexistentTrip_ReturnsTripNotFound()
    {
        await using var ctx = BuildContext(nameof(Submit_NonexistentTrip_ReturnsTripNotFound));
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        var guide = await SeedGuideAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(2, Request(tripId: 777, guide.ProfileId));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.TripNotFound, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    [Theory]
    [InlineData(TripRequestStatus.InProgress)]
    [InlineData(TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.Cancelled)]
    public async Task Submit_TripNotCompleted_ReturnsTripNotCompleted(TripRequestStatus status)
    {
        await using var ctx = BuildContext($"{nameof(Submit_TripNotCompleted_ReturnsTripNotCompleted)}_{status}");
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        var guide = await SeedGuideAsync(ctx);
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 2,
            matchedGuideUserId: guide.UserId, status: status);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(2, Request(300, guide.ProfileId));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.TripNotCompleted, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task Submit_CompletedTripWithoutCompletedAt_ReturnsTripNotCompleted()
    {
        await using var ctx = BuildContext(nameof(Submit_CompletedTripWithoutCompletedAt_ReturnsTripNotCompleted));
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        var guide = await SeedGuideAsync(ctx);
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 2,
            matchedGuideUserId: guide.UserId, completedAtSet: false);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(2, Request(300, guide.ProfileId));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.TripNotCompleted, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Submission — guide eligibility
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Submit_NonexistentGuideProfile_ReturnsGuideNotFound()
    {
        await using var ctx = BuildContext(nameof(Submit_NonexistentGuideProfile_ReturnsGuideNotFound));
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        var guide = await SeedGuideAsync(ctx);
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 2, matchedGuideUserId: guide.UserId);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(2, Request(300, guideProfileId: 999));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.GuideNotFound, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task Submit_GuideNotMatchingMatchedGuideId_ReturnsTouristGuideMismatch()
    {
        await using var ctx = BuildContext(nameof(Submit_GuideNotMatchingMatchedGuideId_ReturnsTouristGuideMismatch));
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        var matchedGuide = await SeedGuideAsync(ctx, userId: 10, profileId: 100, fullName: "Matched Guide");
        var otherGuide = await SeedGuideAsync(ctx, userId: 11, profileId: 101, fullName: "Other Guide");
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 2, matchedGuideUserId: matchedGuide.UserId);
        var svc = BuildService(ctx);

        // Request references a real guide profile that did not serve the trip.
        var result = await svc.SubmitReviewAsync(2, Request(300, otherGuide.ProfileId));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.TouristGuideMismatch, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task Submit_TripWithoutMatchedGuide_ReturnsTouristGuideMismatch()
    {
        await using var ctx = BuildContext(nameof(Submit_TripWithoutMatchedGuide_ReturnsTouristGuideMismatch));
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        var guide = await SeedGuideAsync(ctx);
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 2, matchedGuideUserId: null);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(2, Request(300, guide.ProfileId));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.TouristGuideMismatch, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task Submit_SelfReview_ReturnsSelfReviewNotAllowed()
    {
        // A tourist-role user who also owns a guide profile reviewing their own trip.
        await using var ctx = BuildContext(nameof(Submit_SelfReview_ReturnsSelfReviewNotAllowed));
        await SeedUserAsync(ctx, 5, "Tourist Guide", UserRole.Tourist);
        ctx.GuideProfiles.Add(new GuideProfile
        {
            Id = 105, UserId = 5, Bio = "Dual role", ServiceArea = "Dhaka",
            SupportedTripTypes = "Adventure", ExperienceYears = 3,
            VerificationStatus = VerificationStatus.Verified
        });
        await ctx.SaveChangesAsync();
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 5, matchedGuideUserId: 5);
        var svc = BuildService(ctx);

        var result = await svc.SubmitReviewAsync(5, Request(300, guideProfileId: 105));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.SelfReviewNotAllowed, result.Error);
        Assert.Empty(ctx.Reviews);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Submission — duplicates
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Submit_DuplicateReview_ReturnsDuplicateReview()
    {
        await using var ctx = BuildContext(nameof(Submit_DuplicateReview_ReturnsDuplicateReview));
        var (guide, tripId) = await SeedCompletedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        var first = await svc.SubmitReviewAsync(2, Request(tripId, guide.ProfileId, rating: 5));
        var second = await svc.SubmitReviewAsync(2, Request(tripId, guide.ProfileId, rating: 3));

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(ReviewError.DuplicateReview, second.Error);
        Assert.Single(ctx.Reviews);

        // Average must reflect only the accepted review.
        var profile = await ctx.GuideProfiles.AsNoTracking().SingleAsync(gp => gp.Id == guide.ProfileId);
        Assert.Equal(5m, profile.AverageRating);
    }

    /// <summary>
    /// EF InMemory does not enforce unique indexes, so the unique-index race is simulated:
    /// SaveChanges throws a duplicate-key DbUpdateException and the service must translate
    /// it to a DuplicateReview result instead of surfacing an unhandled exception.
    /// </summary>
    [Fact]
    public async Task Submit_UniqueIndexRace_ReturnsDuplicateReviewInsteadOfThrowing()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Submit_UniqueIndexRace_ReturnsDuplicateReviewInsteadOfThrowing), databaseRoot)
            .Options;

        await using var seedCtx = new ApplicationDbContext(opts);
        var (guide, tripId) = await SeedCompletedScenarioAsync(seedCtx);

        await using var failingCtx = new UniqueViolationDbContext(opts);
        var svc = new ReviewService(failingCtx, NullLogger<ReviewService>.Instance);

        var result = await svc.SubmitReviewAsync(2, Request(tripId, guide.ProfileId, rating: 4));

        Assert.False(result.Success);
        Assert.Equal(ReviewError.DuplicateReview, result.Error);
    }

    private sealed class UniqueViolationDbContext : ApplicationDbContext
    {
        public UniqueViolationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException(
                "An error occurred while saving the entity changes.",
                new Exception("duplicate key value violates unique constraint \"IX_Reviews_TripRequestId\""));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Submission — average rating
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Submit_MultipleReviews_RecalculatesGuideAverageIncrementally()
    {
        await using var ctx = BuildContext(nameof(Submit_MultipleReviews_RecalculatesGuideAverageIncrementally));
        await SeedUserAsync(ctx, 2, "Tourist Two", UserRole.Tourist);
        var guide = await SeedGuideAsync(ctx);
        // Three completed trips served by the same guide.
        await SeedTripRequestAsync(ctx, tripId: 300, touristUserId: 2, matchedGuideUserId: guide.UserId);
        await SeedTripRequestAsync(ctx, tripId: 301, touristUserId: 2, matchedGuideUserId: guide.UserId);
        await SeedTripRequestAsync(ctx, tripId: 302, touristUserId: 2, matchedGuideUserId: guide.UserId);
        var svc = BuildService(ctx);

        await svc.SubmitReviewAsync(2, Request(300, guide.ProfileId, rating: 5));
        var afterFirst = await ctx.GuideProfiles.AsNoTracking().SingleAsync(gp => gp.Id == guide.ProfileId);
        Assert.Equal(5m, afterFirst.AverageRating);

        await svc.SubmitReviewAsync(2, Request(301, guide.ProfileId, rating: 4));
        var afterSecond = await ctx.GuideProfiles.AsNoTracking().SingleAsync(gp => gp.Id == guide.ProfileId);
        Assert.Equal(4.5m, afterSecond.AverageRating);

        await svc.SubmitReviewAsync(2, Request(302, guide.ProfileId, rating: 4));
        var afterThird = await ctx.GuideProfiles.AsNoTracking().SingleAsync(gp => gp.Id == guide.ProfileId);

        // (5 + 4 + 4) / 3 = 4.333… → 4.33 with two-decimal half-away-from-zero rounding.
        Assert.Equal(4.33m, afterThird.AverageRating);

        var rating = await svc.GetGuideRatingAsync(guide.ProfileId);
        Assert.NotNull(rating);
        Assert.Equal(4.33m, rating!.AverageRating);
        Assert.Equal(3, rating.ReviewCount);
    }

    [Fact]
    public async Task Submit_OnInMemoryWithoutTransactions_PersistsReviewAndProfile()
    {
        await using var ctx = BuildContext(nameof(Submit_OnInMemoryWithoutTransactions_PersistsReviewAndProfile));
        var (guide, tripId) = await SeedCompletedScenarioAsync(ctx);
        var svc = BuildService(ctx);

        // EF InMemory reports IsRelational() == false, so the service must skip the
        // explicit transaction and still persist both the review and the profile change.
        Assert.False(ctx.Database.IsRelational());

        var result = await svc.SubmitReviewAsync(2, Request(tripId, guide.ProfileId, rating: 5));

        Assert.True(result.Success);
        Assert.Single(ctx.Reviews);
        var profile = await ctx.GuideProfiles.AsNoTracking().SingleAsync(gp => gp.Id == guide.ProfileId);
        Assert.Equal(5m, profile.AverageRating);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Listing
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Seeds a guide plus one completed trip and review per entry. CreatedAt values are
    /// explicit so ordering assertions are deterministic.
    /// </summary>
    private static async Task SeedReviewsAsync(
        ApplicationDbContext ctx, int guideProfileId, params (int TouristId, int Rating, DateTime CreatedAt)[] reviews)
    {
        foreach (var (touristId, rating, createdAt) in reviews)
        {
            await SeedUserAsync(ctx, touristId, $"Tourist {touristId}", UserRole.Tourist);
        }

        var tripId = 500;
        foreach (var (touristId, rating, createdAt) in reviews)
        {
            ctx.Reviews.Add(new Review
            {
                TouristId = touristId,
                GuideId = guideProfileId,
                TripRequestId = tripId++,
                Rating = rating,
                Comment = $"Review by {touristId}",
                CreatedAt = createdAt
            });
        }
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task List_ExistingGuide_ReturnsReviewsNewestFirst()
    {
        await using var ctx = BuildContext(nameof(List_ExistingGuide_ReturnsReviewsNewestFirst));
        var guide = await SeedGuideAsync(ctx, userId: 10, profileId: 100, fullName: "Guide One");
        var baseTime = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        await SeedReviewsAsync(ctx, guide.ProfileId,
            (2, 5, baseTime.AddHours(1)),
            (3, 4, baseTime.AddHours(3)),
            (4, 3, baseTime.AddHours(2)));
        var svc = BuildService(ctx);

        var result = await svc.GetReviewsByGuideAsync(guide.ProfileId);

        Assert.True(result.Success);
        Assert.Equal(ReviewError.None, result.Error);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalPages);

        var items = result.Data!;
        // Newest first: tourist 3 (+3h), tourist 4 (+2h), tourist 2 (+1h).
        Assert.Equal(new[] { 3, 4, 2 }, items.Select(r => r.TouristId).ToArray());
        Assert.All(items, r => Assert.Equal("Guide One", r.GuideName));
        Assert.All(items, r => Assert.Equal(guide.ProfileId, r.GuideId));
        Assert.Equal("Tourist 3", items[0].TouristName);
    }

    [Fact]
    public async Task List_TiedCreatedAt_OrdersByIdDescending()
    {
        await using var ctx = BuildContext(nameof(List_TiedCreatedAt_OrdersByIdDescending));
        var guide = await SeedGuideAsync(ctx);
        var sameTime = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        await SeedReviewsAsync(ctx, guide.ProfileId,
            (2, 5, sameTime),
            (3, 4, sameTime),
            (4, 3, sameTime));
        var svc = BuildService(ctx);

        var result = await svc.GetReviewsByGuideAsync(guide.ProfileId);

        Assert.True(result.Success);
        var items = result.Data!;
        // Stable secondary ordering: newest CreatedAt first, then highest Id.
        Assert.True(items[0].Id > items[1].Id);
        Assert.True(items[1].Id > items[2].Id);
    }

    [Fact]
    public async Task List_Pagination_ReturnsCorrectPageAndTotals()
    {
        await using var ctx = BuildContext(nameof(List_Pagination_ReturnsCorrectPageAndTotals));
        var guide = await SeedGuideAsync(ctx);
        var baseTime = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        await SeedReviewsAsync(ctx, guide.ProfileId,
            (2, 5, baseTime.AddHours(1)),
            (3, 5, baseTime.AddHours(2)),
            (4, 4, baseTime.AddHours(3)),
            (5, 4, baseTime.AddHours(4)),
            (6, 3, baseTime.AddHours(5)));
        var svc = BuildService(ctx);

        var page1 = await svc.GetReviewsByGuideAsync(guide.ProfileId, page: 1, pageSize: 2);
        var page2 = await svc.GetReviewsByGuideAsync(guide.ProfileId, page: 2, pageSize: 2);
        var page3 = await svc.GetReviewsByGuideAsync(guide.ProfileId, page: 3, pageSize: 2);

        Assert.True(page1.Success);
        Assert.Equal(2, page1.Data!.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(new[] { 6, 5 }, page1.Data.Select(r => r.TouristId).ToArray());

        Assert.Equal(2, page2.Data!.Count);
        Assert.Equal(new[] { 4, 3 }, page2.Data.Select(r => r.TouristId).ToArray());

        Assert.Single(page3.Data!);
        Assert.Equal(2, page3.Data![0].TouristId);
    }

    [Fact]
    public async Task List_PageBeyondRange_ReturnsEmptyPageWithTotals()
    {
        await using var ctx = BuildContext(nameof(List_PageBeyondRange_ReturnsEmptyPageWithTotals));
        var guide = await SeedGuideAsync(ctx);
        await SeedReviewsAsync(ctx, guide.ProfileId, (2, 5, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)));
        var svc = BuildService(ctx);

        var result = await svc.GetReviewsByGuideAsync(guide.ProfileId, page: 5, pageSize: 10);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task List_GuideWithoutReviews_ReturnsEmptySuccess()
    {
        await using var ctx = BuildContext(nameof(List_GuideWithoutReviews_ReturnsEmptySuccess));
        var guide = await SeedGuideAsync(ctx);
        var svc = BuildService(ctx);

        var result = await svc.GetReviewsByGuideAsync(guide.ProfileId);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task List_NonexistentGuide_ReturnsGuideNotFound()
    {
        await using var ctx = BuildContext(nameof(List_NonexistentGuide_ReturnsGuideNotFound));
        var svc = BuildService(ctx);

        var result = await svc.GetReviewsByGuideAsync(guideProfileId: 999);

        Assert.False(result.Success);
        Assert.Equal(ReviewError.GuideNotFound, result.Error);
        Assert.Null(result.Data);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Guide rating lookup
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Rating_GuideWithoutReviews_PreservesNullAverage()
    {
        await using var ctx = BuildContext(nameof(Rating_GuideWithoutReviews_PreservesNullAverage));
        var guide = await SeedGuideAsync(ctx);
        var svc = BuildService(ctx);

        var rating = await svc.GetGuideRatingAsync(guide.ProfileId);

        Assert.NotNull(rating);
        Assert.Equal(guide.ProfileId, rating!.GuideId);
        Assert.Null(rating.AverageRating);
        Assert.Equal(0, rating.ReviewCount);
    }

    [Fact]
    public async Task Rating_GuideWithReviews_ReturnsAverageAndCount()
    {
        await using var ctx = BuildContext(nameof(Rating_GuideWithReviews_ReturnsAverageAndCount));
        var guide = await SeedGuideAsync(ctx);
        var baseTime = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        await SeedReviewsAsync(ctx, guide.ProfileId,
            (2, 5, baseTime),
            (3, 4, baseTime.AddHours(1)),
            (4, 3, baseTime.AddHours(2)));
        var svc = BuildService(ctx);

        var rating = await svc.GetGuideRatingAsync(guide.ProfileId);

        Assert.NotNull(rating);
        Assert.Equal(guide.ProfileId, rating!.GuideId);
        Assert.Equal(4m, rating.AverageRating);
        Assert.Equal(3, rating.ReviewCount);
    }

    [Fact]
    public async Task Rating_NonexistentGuide_ReturnsNull()
    {
        await using var ctx = BuildContext(nameof(Rating_NonexistentGuide_ReturnsNull));
        var svc = BuildService(ctx);

        var rating = await svc.GetGuideRatingAsync(guideProfileId: 999);

        Assert.Null(rating);
    }
}
