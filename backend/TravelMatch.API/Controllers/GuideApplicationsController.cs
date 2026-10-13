using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.GuideApplications;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Guide")]
public class GuideApplicationsController : ControllerBase
{
    private readonly IGuideApplicationService _guideApplicationService;

    public GuideApplicationsController(
        IGuideApplicationService guideApplicationService)
    {
        _guideApplicationService = guideApplicationService;
    }

    [HttpGet("trips")]
    public async Task<IActionResult> GetAvailableTrips(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _guideApplicationService
            .GetAvailableTripsAsync(guideId, page, pageSize);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            GuideApplicationError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpGet("trips/{tripId:int}")]
    public async Task<IActionResult> GetTripDetail(int tripId)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _guideApplicationService
            .GetTripDetailAsync(guideId, tripId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            GuideApplicationError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            GuideApplicationError.TripNotFound =>
                NotFound(result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost("trips/{tripId:int}/apply")]
    public async Task<IActionResult> Apply(
        int tripId,
        [FromBody] CreateGuideApplicationDto request)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
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

        var result = await _guideApplicationService
            .ApplyAsync(guideId, tripId, request);

        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            GuideApplicationError.GuideNotVerified =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            GuideApplicationError.TripNotFound =>
                NotFound(result),

            GuideApplicationError.TripNotAcceptingApplications =>
                StatusCode(StatusCodes.Status409Conflict, result),

            GuideApplicationError.AlreadyApplied =>
                StatusCode(StatusCodes.Status409Conflict, result),

            GuideApplicationError.InvalidPrice =>
                BadRequest(result),

            GuideApplicationError.MessageRequired =>
                BadRequest(result),

            GuideApplicationError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            GuideApplicationError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetMyApplications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _guideApplicationService
            .GetMyApplicationsAsync(guideId, page, pageSize);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            GuideApplicationError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            GuideApplicationError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetApplication(int id)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _guideApplicationService
            .GetApplicationAsync(guideId, id);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            GuideApplicationError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            _ =>
                NotFound(result)
        };
    }

    private bool TryGetGuideId(out int guideId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(userIdClaim, out guideId);
    }
}
