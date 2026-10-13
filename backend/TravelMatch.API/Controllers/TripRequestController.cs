
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var touristId))
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

return result.Error switch
{
    TripRequestError.Unauthorized =>
        StatusCode(StatusCodes.Status403Forbidden, result),

    TripRequestError.TouristNotFound =>
        StatusCode(StatusCodes.Status404NotFound, result),

    _ =>
        BadRequest(result)
};


    }

    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.CancelAsync(
            touristId,
            id);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripRequestError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripRequestError.NotTripRequestOwner =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripRequestError.TouristNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            TripRequestError.TripRequestNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            TripRequestError.AlreadyCancelled =>
                Conflict(result),

            TripRequestError.TripRequestCompleted =>
                Conflict(result),

            _ =>
                BadRequest(result)
        };
    }
}

