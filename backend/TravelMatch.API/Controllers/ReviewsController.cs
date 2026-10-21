using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Reviews;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

/// <summary>
/// Reviews and ratings for guides on completed trips. Submissions are tourist-only;
/// all endpoints require an authenticated user.
/// </summary>
[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Submits a review for a completed trip. The tourist identity comes from the JWT,
    /// never from the request body.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> SubmitReview([FromBody] CreateReviewRequestDto request)
    {
        if (!TryGetTouristId(out var touristUserId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _reviewService.SubmitReviewAsync(touristUserId, request);

        if (result.Success)
            return StatusCode(StatusCodes.Status201Created, result);

        return result.Error switch
        {
            ReviewError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ReviewError.TripNotFound or ReviewError.GuideNotFound =>
                NotFound(result),

            ReviewError.DuplicateReview or ReviewError.TripNotCompleted =>
                StatusCode(StatusCodes.Status409Conflict, result),

            ReviewError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Returns a page of reviews for a guide profile, newest first.
    /// </summary>
    [HttpGet("guide/{guideProfileId:int}")]
    public async Task<IActionResult> GetGuideReviews(
        [FromRoute] int guideProfileId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new
            {
                success = false,
                message = "page must be at least 1 and pageSize must be between 1 and 100."
            });

        var result = await _reviewService.GetReviewsByGuideAsync(guideProfileId, page, pageSize);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            ReviewError.GuideNotFound => NotFound(result),
            ReviewError.ServerError => StatusCode(StatusCodes.Status500InternalServerError, result),
            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Returns the guide's average rating and review count. The average is null when
    /// the guide has no reviews.
    /// </summary>
    [HttpGet("guide/{guideProfileId:int}/rating")]
    public async Task<IActionResult> GetGuideRating([FromRoute] int guideProfileId)
    {
        var rating = await _reviewService.GetGuideRatingAsync(guideProfileId);

        if (rating is null)
            return NotFound(new { success = false, message = "Guide profile not found." });

        return Ok(rating);
    }

    private bool TryGetTouristId(out int touristUserId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out touristUserId);
    }
}
