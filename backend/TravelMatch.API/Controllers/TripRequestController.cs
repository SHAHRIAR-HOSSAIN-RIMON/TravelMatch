using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/trip-requests")]
[Authorize(Roles = "Tourist")]
public class TripRequestsController : ControllerBase
{
    private readonly ITripRequestService _tripRequestService;

    public TripRequestsController(ITripRequestService tripRequestService)
    {
        _tripRequestService = tripRequestService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripRequestDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.CreateAsync(
            touristId,
            request);

        if (result.Success)
        {
            return StatusCode(
                StatusCodes.Status201Created,
                result);
        }

        return ToActionResult(result);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        [FromQuery] MyTripRequestQueryDto query)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.GetMineAsync(touristId, query);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripRequestError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpGet("mine/{id:int}")]
    public async Task<IActionResult> GetMineById(int id)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.GetByIdAsync(touristId, id);

        if (result.Success)
        {
            return Ok(result);
        }

        return ToActionResult(result);
    }

    [HttpPut("mine/{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateTripRequestDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.UpdateAsync(
            touristId,
            id,
            request);

        if (result.Success)
        {
            return Ok(result);
        }

        return ToActionResult(result);
    }

    [HttpPost("mine/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.CancelAsync(touristId, id);

        if (result.Success)
        {
            return Ok(result);
        }

        return ToActionResult(result);
    }

    private IActionResult ToActionResult(TripRequestResultDto result)
    {
        return result.Error switch
        {
            TripRequestError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripRequestError.TouristNotFound or TripRequestError.TripRequestNotFound =>
                NotFound(result),

            TripRequestError.TripRequestNotOpen =>
                StatusCode(StatusCodes.Status409Conflict, result),

            TripRequestError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    private bool TryGetTouristId(out int touristId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(userIdClaim, out touristId);
    }
}