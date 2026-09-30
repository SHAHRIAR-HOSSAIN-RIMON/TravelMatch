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

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripRequestDto request)
    {
        var touristId = GetAuthenticatedUserId();

        if (touristId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.CreateAsync(
            touristId.Value,
            request);

        return result.Success
            ? StatusCode(StatusCodes.Status201Created, result)
            : MapError(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetMyTripRequest(int id)
    {
        var touristId = GetAuthenticatedUserId();

        if (touristId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.GetMyTripRequestAsync(
            touristId.Value,
            id);

        return result.Success ? Ok(result) : MapError(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMyTripRequest(
        int id,
        [FromBody] UpdateTripRequestDto request)
    {
        var touristId = GetAuthenticatedUserId();

        if (touristId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.UpdateMyTripRequestAsync(
            touristId.Value,
            id,
            request);

        return result.Success ? Ok(result) : MapError(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelMyTripRequest(int id)
    {
        var touristId = GetAuthenticatedUserId();

        if (touristId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.CancelMyTripRequestAsync(
            touristId.Value,
            id);

        return result.Success ? Ok(result) : MapError(result);
    }

    private int? GetAuthenticatedUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }

    private IActionResult MapError(TripRequestResultDto result)
    {
        return result.Error switch
        {
            TripRequestError.TripRequestNotFound =>
                NotFound(result),

            TripRequestError.NotOwner =>
                NotFound(result),

            TripRequestError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripRequestError.TouristNotFound =>
                NotFound(result),

            TripRequestError.InvalidStatus =>
                Conflict(result),

            TripRequestError.InvalidStartDate =>
                BadRequest(result),

            TripRequestError.InvalidEndDate =>
                BadRequest(result),

            TripRequestError.InvalidDestination =>
                BadRequest(result),

            TripRequestError.StartDateInPast =>
                BadRequest(result),

            TripRequestError.EndDateBeforeStartDate =>
                BadRequest(result),

            TripRequestError.InvalidNumberOfTravelers =>
                BadRequest(result),

            TripRequestError.InvalidBudget =>
                BadRequest(result),

            _ => BadRequest(result)
        };
    }
}
