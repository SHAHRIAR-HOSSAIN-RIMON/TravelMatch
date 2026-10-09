using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.ItineraryTracking;
using TravelMatch.API.Hubs;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/itinerary-tracking")]
[Authorize(Roles = "Guide,Tourist")]
public class ItineraryTrackingController : ControllerBase
{
    private readonly IItineraryTrackingService _trackingService;
    private readonly IHubContext<ItineraryTrackingHub> _hubContext;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ItineraryTrackingController> _logger;

    public ItineraryTrackingController(
        IItineraryTrackingService trackingService,
        IHubContext<ItineraryTrackingHub> hubContext,
        ApplicationDbContext context,
        ILogger<ItineraryTrackingController> logger)
    {
        _trackingService = trackingService;
        _hubContext = hubContext;
        _context = context;
        _logger = logger;
    }

    [HttpGet("proposals/{proposalId:int}")]
    public async Task<IActionResult> GetItineraryTracking(int proposalId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _trackingService.GetItineraryTrackingAsync(userId, proposalId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            ItineraryTrackingError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryTrackingError.ProposalNotFound =>
                NotFound(result),

            ItineraryTrackingError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPut("activities/{activityId:int}/status")]
    [Authorize(Roles = "Guide")]
    public async Task<IActionResult> UpdateActivityStatus(
        int activityId,
        [FromBody] UpdateActivityStatusDto request)
    {
        if (!TryGetUserId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                message = "Validation failed.",
                errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => e.ErrorMessage)
                            .ToArray())
            });
        }

        var result = await _trackingService.UpdateActivityStatusAsync(guideId, activityId, request.Status);

        if (result.Success && result.Data is not null)
        {
            await _hubContext.Clients.Group($"proposal-{result.Data.ProposalId}")
                .SendAsync("ActivityStatusUpdated", result.Data);

            return Ok(result);
        }

        return result.Error switch
        {
            ItineraryTrackingError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryTrackingError.ActivityNotFound =>
                NotFound(result),

            ItineraryTrackingError.InvalidStatus =>
                BadRequest(result),

            ItineraryTrackingError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost("activities")]
    [Authorize(Roles = "Guide")]
    public async Task<IActionResult> CreateActivity(
        [FromBody] CreateItineraryActivityDto request)
    {
        if (!TryGetUserId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                message = "Validation failed.",
                errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => e.ErrorMessage)
                            .ToArray())
            });
        }

        var result = await _trackingService.CreateActivityAsync(guideId, request);

        if (result.Success && result.Data is not null)
        {
            await _hubContext.Clients.Group($"proposal-{result.Data.ProposalId}")
                .SendAsync("ActivityCreated", result.Data);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            ItineraryTrackingError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryTrackingError.DayNotFound =>
                NotFound(result),

            ItineraryTrackingError.InvalidStatus =>
                BadRequest(result),

            ItineraryTrackingError.InvalidActivity =>
                BadRequest(result),

            ItineraryTrackingError.DuplicateOrderIndex =>
                Conflict(result),

            ItineraryTrackingError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpDelete("activities/{activityId:int}")]
    [Authorize(Roles = "Guide")]
    public async Task<IActionResult> DeleteActivity(int activityId)
    {
        if (!TryGetUserId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var proposalId = await GetProposalIdForActivity(activityId);

        var result = await _trackingService.DeleteActivityAsync(guideId, activityId);

        if (result.Success)
        {
            if (proposalId > 0)
            {
                await _hubContext.Clients.Group($"proposal-{proposalId}")
                    .SendAsync("ActivityDeleted", new { activityId });
            }

            return Ok(result);
        }

        return result.Error switch
        {
            ItineraryTrackingError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryTrackingError.ActivityNotFound =>
                NotFound(result),

            ItineraryTrackingError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    private async Task<int> GetProposalIdForActivity(int activityId)
    {
        return await _context.ItineraryActivities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => a.ItineraryDay.ProposalId)
            .FirstOrDefaultAsync();
    }

    private bool TryGetUserId(out int userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out userId);
    }
}