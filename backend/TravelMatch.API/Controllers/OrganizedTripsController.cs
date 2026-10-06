using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.OrganizedTrips;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Organizer")]
public class OrganizedTripsController : ControllerBase
{
    private readonly IOrganizedTripService _organizedTripService;

    public OrganizedTripsController(IOrganizedTripService organizedTripService)
    {
        _organizedTripService = organizedTripService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizedTripDto request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _organizedTripService.CreateAsync(userId, request);

        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            OrganizedTripError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            OrganizedTripError.OrganizerNotFound => StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("{organizedTripId:int}/publish")]
    public async Task<IActionResult> Publish(int organizedTripId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _organizedTripService.PublishAsync(userId, organizedTripId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            OrganizedTripError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            OrganizedTripError.OrganizerNotFound => StatusCode(StatusCodes.Status403Forbidden, result),
            OrganizedTripError.OrganizedTripNotFound => NotFound(result),
            _ => BadRequest(result)
        };
    }
}
