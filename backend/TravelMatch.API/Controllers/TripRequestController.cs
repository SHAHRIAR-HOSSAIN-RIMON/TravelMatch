
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Tourist")]
public class TripRequestsController : ControllerBase
{
    private readonly ITripRequestService _tripRequestService;

    public TripRequestsController(ITripRequestService tripRequestService)
    {
        _tripRequestService = tripRequestService;
    }

    /// <summary>List the authenticated tourist's own trip requests.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMy()
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.GetMyRequestsAsync(touristId);

        return Ok(result);
    }

    /// <summary>Get a single trip request owned by the authenticated tourist.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne([FromRoute] int id)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.GetMyRequestAsync(touristId, id);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripRequestError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripRequestError.TouristNotFound =>
                NotFound(result),

            _ => BadRequest(result)
        };
    }

    /// <summary>Create a new trip request.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTripRequestDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.CreateAsync(touristId, request);

        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            TripRequestError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripRequestError.TouristNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Update a Pending trip request. Only allowed while the request is in Pending status.
    /// Send only the fields you want to change; unset fields keep their current value.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        [FromRoute] int id,
        [FromBody] UpdateTripRequestDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.UpdateAsync(touristId, id, request);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripRequestError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripRequestError.TouristNotFound =>
                NotFound(result),

            TripRequestError.StartDateInPast or
            TripRequestError.EndDateBeforeStartDate or
            TripRequestError.InvalidBudget or
            TripRequestError.InvalidNumberOfTravelers =>
                BadRequest(result),

            _ => BadRequest(result)
        };
    }

    private bool TryGetTouristId(out int touristId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out touristId);
    }
}

