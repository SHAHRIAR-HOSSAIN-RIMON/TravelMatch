using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Reviews;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(ApplicationDbContext context, ILogger<ReviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Submit review ─────────────────────────────────────────────────────────

    public async Task<ReviewResultDto> SubmitReviewAsync(int touristUserId, CreateReviewRequestDto request)
    {
        // Defensive rating validation, independent of API model validation.
        if (request.Rating < 1 || request.Rating > 5)
            return Failure(ReviewError.InvalidRating, "Rating must be between 1 and 5.");

        if (request.TripRequestId <= 0 || request.GuideId <= 0)
            return Failure(ReviewError.TripNotFound, "A valid trip request and guide are required.");

        var tourist = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == touristUserId);

        if (tourist is null || tourist.Role != UserRole.Tourist)
            return Failure(ReviewError.Unauthorized, "Only a tourist may submit a review.");

        var trip = await _context.TripRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TripRequestId);

        if (trip is null)
            return Failure(ReviewError.TripNotFound, "Trip request not found.");

        if (trip.Status != TripRequestStatus.Completed || trip.CompletedAt is null)
            return Failure(ReviewError.TripNotCompleted, "Reviews can only be written for completed trips.");

        if (trip.TouristId != touristUserId)
            return Failure(ReviewError.Unauthorized, "You can only review your own trip.");

        // Review.GuideId references GuideProfile.Id, while TripRequest.MatchedGuideId
        // references the guide's User.Id — resolve the profile and compare through UserId.
        var guideProfile = await _context.GuideProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.GuideId);

        if (guideProfile is null)
            return Failure(ReviewError.GuideNotFound, "Guide profile not found.");

        if (guideProfile.UserId == touristUserId)
            return Failure(ReviewError.SelfReviewNotAllowed, "You cannot review yourself.");

        if (trip.MatchedGuideId != guideProfile.UserId)
            return Failure(ReviewError.TouristGuideMismatch, "This guide did not serve the completed trip.");

        if (await _context.Reviews.AnyAsync(r => r.TripRequestId == request.TripRequestId))
            return Failure(ReviewError.DuplicateReview, "This trip has already been reviewed.");

        var guideName = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == guideProfile.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? string.Empty;

        try
        {
            // Relational-only transaction; EF InMemory tests run without one.
            var tx = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync() : null;
            await using var _ = tx;

            var review = new Review
            {
                TouristId = touristUserId,
                GuideId = guideProfile.Id,
                TripRequestId = trip.Id,
                Rating = request.Rating,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Keep the denormalized profile average consistent with persisted reviews.
            guideProfile = await _context.GuideProfiles
                .FirstOrDefaultAsync(g => g.Id == guideProfile.Id);

            await RecalculateAverageRatingAsync(guideProfile!);
            await _context.SaveChangesAsync();

            if (tx is not null) await tx.CommitAsync();

            _logger.LogInformation(
                "Review {ReviewId} created for TripRequest {TripRequestId} by user {TouristUserId}.",
                review.Id, trip.Id, touristUserId);

            return new ReviewResultDto
            {
                Success = true,
                Error = ReviewError.None,
                Message = "Review submitted successfully.",
                Data = ToResponse(review, tourist.FullName, guideName)
            };
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Lost the race on the one-review-per-trip unique index.
            _logger.LogWarning(ex,
                "Duplicate review race on TripRequest {TripRequestId} by user {TouristUserId}.",
                request.TripRequestId, touristUserId);
            return Failure(ReviewError.DuplicateReview, "This trip has already been reviewed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error submitting review for TripRequest {TripRequestId}.", request.TripRequestId);
            return Failure(ReviewError.ServerError, "An unexpected error occurred. Please retry.");
        }
    }

    // ── List reviews for a guide ──────────────────────────────────────────────

    public async Task<ReviewListResultDto> GetReviewsByGuideAsync(int guideProfileId, int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var guideProfile = await _context.GuideProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == guideProfileId);

        if (guideProfile is null)
        {
            return new ReviewListResultDto
            {
                Success = false,
                Error = ReviewError.GuideNotFound,
                Message = "Guide profile not found.",
                Page = page,
                PageSize = pageSize
            };
        }

        // GuideName resolves through GuideProfile.UserId to User.FullName.
        var guideName = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == guideProfile.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? string.Empty;

        // Tourist display names come from the User behind Review.TouristId; only
        // public fields are projected so no sensitive user data leaves the service.
        var query = _context.Reviews
            .AsNoTracking()
            .Where(r => r.GuideId == guideProfileId)
            .Join(
                _context.Users.AsNoTracking(),
                r => r.TouristId,
                u => u.Id,
                (r, u) => new ReviewResponseDto
                {
                    Id            = r.Id,
                    TouristId     = r.TouristId,
                    TouristName   = u.FullName,
                    GuideId       = r.GuideId,
                    GuideName     = guideName,
                    TripRequestId = r.TripRequestId,
                    Rating        = r.Rating,
                    Comment       = r.Comment,
                    CreatedAt     = r.CreatedAt,
                    UpdatedAt     = r.UpdatedAt
                });

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(dto => dto.CreatedAt)
            .ThenByDescending(dto => dto.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ReviewListResultDto
        {
            Success = true,
            Error = ReviewError.None,
            Message = "Guide reviews retrieved successfully.",
            Data = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    // ── Guide rating summary ──────────────────────────────────────────────────

    public async Task<GuideRatingDto?> GetGuideRatingAsync(int guideProfileId)
    {
        var guideExists = await _context.GuideProfiles
            .AsNoTracking()
            .AnyAsync(g => g.Id == guideProfileId);

        if (!guideExists)
            return null;

        var ratings = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.GuideId == guideProfileId)
            .Select(r => r.Rating)
            .ToListAsync();

        return new GuideRatingDto
        {
            GuideId = guideProfileId,
            AverageRating = ComputeAverage(ratings),
            ReviewCount = ratings.Count
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes GuideProfile.AverageRating from all persisted reviews for the guide.
    /// Null when the guide has no reviews — never a silent zero.
    /// </summary>
    private async Task RecalculateAverageRatingAsync(GuideProfile guideProfile)
    {
        var ratings = await _context.Reviews
            .Where(r => r.GuideId == guideProfile.Id)
            .Select(r => r.Rating)
            .ToListAsync();

        guideProfile.AverageRating = ComputeAverage(ratings);
    }

    // Decimal arithmetic with two-decimal rounding, matching the project's precision
    // convention for stored decimal values.
    private static decimal? ComputeAverage(List<int> ratings) =>
        ratings.Count == 0
            ? null
            : Math.Round(ratings.Sum(r => (decimal)r) / ratings.Count, 2, MidpointRounding.AwayFromZero);

    private static ReviewResponseDto ToResponse(Review review, string touristName, string guideName) => new()
    {
        Id            = review.Id,
        TouristId     = review.TouristId,
        TouristName   = touristName,
        GuideId       = review.GuideId,
        GuideName     = guideName,
        TripRequestId = review.TripRequestId,
        Rating        = review.Rating,
        Comment       = review.Comment,
        CreatedAt     = review.CreatedAt,
        UpdatedAt     = review.UpdatedAt
    };

    private static ReviewResultDto Failure(ReviewError error, string message) =>
        new() { Success = false, Error = error, Message = message };

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx &&
            pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
            return true;

        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("IX_Reviews_TripRequestId", StringComparison.OrdinalIgnoreCase);
    }
}
