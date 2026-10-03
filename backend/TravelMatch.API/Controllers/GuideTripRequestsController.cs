using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/trip-requests")]
[Authorize(Roles = "Guide")]
public class GuideTripRequestsController : ControllerBase
{
    private readonly IGuideTripRequestService _guideTripRequestService;

    public GuideTripRequestsController(
        IGuideTripRequestService guideTripRequestService)
    {
        _guideTripRequestService = guideTripRequestService;
    }

    [HttpGet("open")]
    public async Task<IActionResult> GetOpenTripRequests(
        [FromQuery] OpenTripRequestQueryDto query)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _guideTripRequestService
            .GetOpenTripRequestsAsync(guideId, query);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            OpenTripRequestError.GuideNotFound =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            OpenTripRequestError.InvalidFilter =>
                BadRequest(result),

            OpenTripRequestError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                StatusCode(StatusCodes.Status404NotFound, result)
        };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOpenTripRequestDetail(int id)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _guideTripRequestService
            .GetOpenTripRequestDetailAsync(guideId, id);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            OpenTripRequestError.GuideNotFound =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            OpenTripRequestError.TripRequestNotFound =>
                NotFound(result),

            OpenTripRequestError.TripRequestNotAvailable =>
                StatusCode(StatusCodes.Status409Conflict, result),

            OpenTripRequestError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    private bool TryGetGuideId(out int guideId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(userIdClaim, out guideId);
    }
}