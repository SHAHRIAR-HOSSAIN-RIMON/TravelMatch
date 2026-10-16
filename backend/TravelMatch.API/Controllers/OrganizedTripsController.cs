using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.OrganizedTrips;
using TravelMatch.API.DTOs.Registrations;
using TravelMatch.API.DTOs.TripRequests;
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

    // ── Public tourist-facing browse endpoints ───────────────────────────────

    /// <summary>
    /// Returns all trips currently open for registration with remaining spots.
    /// Accessible to anyone (no auth required).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableTrips()
    {
        var result = await _organizedTripService.GetAvailableTripsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Returns the full detail of a single trip open for registration.
    /// Accessible to anyone (no auth required).
    /// </summary>
    [HttpGet("{organizedTripId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableTripDetail(int organizedTripId)
    {
        var result = await _organizedTripService.GetAvailableTripDetailAsync(organizedTripId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            OrganizedTripError.OrganizedTripNotFound => NotFound(result),
            _ => BadRequest(result)
        };
    }

    // ── Organizer-only endpoints ─────────────────────────────────────────────

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

    /// <summary>
    /// Allows an organizer to cancel a specific registration on their own trip.
    /// </summary>
    [HttpPost("{organizedTripId:int}/registrations/{registrationId:int}/cancel")]
    public async Task<IActionResult> CancelRegistrationAsOrganizer(
        int organizedTripId,
        int registrationId,
        [FromBody] TransitionTripRequestDto request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _organizedTripService.CancelRegistrationAsOrganizerAsync(
            userId, organizedTripId, registrationId, request.CancellationReason);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            RegistrationError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            RegistrationError.OrganizerNotFound => StatusCode(StatusCodes.Status403Forbidden, result),
            RegistrationError.OrganizedTripNotFound => NotFound(result),
            RegistrationError.RegistrationNotFound => NotFound(result),
            _ => BadRequest(result)
        };
    }
}
