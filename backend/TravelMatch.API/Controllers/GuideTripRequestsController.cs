using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/guide/trip-requests")]
[Authorize(Roles = "Guide")]
public class GuideTripRequestsController : ControllerBase
{
    private readonly ITripRequestService _tripRequestService;

    public GuideTripRequestsController(ITripRequestService tripRequestService)
    {
        _tripRequestService = tripRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] string? destination,
        [FromQuery] string? tripType,
        [FromQuery] DateOnly? travelDateFrom,
        [FromQuery] DateOnly? travelDateTo,
        [FromQuery] decimal? minBudget,
        [FromQuery] decimal? maxBudget,
        [FromQuery] string? sortBy)
    {
        if (travelDateFrom.HasValue &&
            travelDateTo.HasValue &&
            travelDateFrom > travelDateTo)
        {
            return BadRequest(new
            {
                message = "Travel date range is invalid."
            });
        }

        if (minBudget < 0 || maxBudget < 0 ||
            (minBudget.HasValue && maxBudget.HasValue && minBudget > maxBudget))
        {
            return BadRequest(new
            {
                message = "Budget range is invalid."
            });
        }

        var requests = await _tripRequestService.GetAvailableAsync(
            destination,
            tripType,
            travelDateFrom,
            travelDateTo,
            minBudget,
            maxBudget,
            sortBy);

        return Ok(requests);
    }

    [HttpGet("verification")]
    public async Task<IActionResult> GetVerificationStatus()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userId, out var guideUserId))
        {
            return Unauthorized();
        }

        var status = await _tripRequestService.GetGuideVerificationStatusAsync(guideUserId);
        return Ok(new
        {
            status = status?.ToString() ?? VerificationStatus.Pending.ToString(),
            isVerified = status == VerificationStatus.Verified
        });
    }

    [HttpGet("{tripRequestId:int}")]
    public async Task<IActionResult> GetAvailableById(int tripRequestId)
    {
        var request = await _tripRequestService.GetAvailableByIdAsync(
            tripRequestId);

        return request is null ? NotFound() : Ok(request);
    }
}