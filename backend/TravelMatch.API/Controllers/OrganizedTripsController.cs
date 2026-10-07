using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.OrganizedTrips;
using TravelMatch.API.DTOs.Registrations;
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

    [HttpGet("my")]
    public async Task<IActionResult> GetMyTrips()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _organizedTripService.GetMyTripsAsync(userId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            OrganizedTripError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            OrganizedTripError.OrganizerNotFound => StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }

    [HttpGet("{organizedTripId:int}/registrations")]
    public async Task<IActionResult> GetRegistrations(int organizedTripId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _organizedTripService.GetRegistrationsAsync(userId, organizedTripId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            RegistrationError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            RegistrationError.OrganizerNotFound => StatusCode(StatusCodes.Status403Forbidden, result),
            RegistrationError.OrganizedTripNotFound => NotFound(result),
            _ => BadRequest(result)
        };
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

    [HttpPut("{organizedTripId:int}")]
    public async Task<IActionResult> Update(int organizedTripId, [FromBody] CreateOrganizedTripDto request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _organizedTripService.UpdateAsync(userId, organizedTripId, request);

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

    [HttpPost("{organizedTripId:int}/cancel")]
    public async Task<IActionResult> Cancel(int organizedTripId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _organizedTripService.CancelAsync(userId, organizedTripId);

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
