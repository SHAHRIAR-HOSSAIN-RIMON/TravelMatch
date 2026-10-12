using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Registrations;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/organized-trips/{organizedTripId:int}/registrations")]
[Authorize(Roles = "Tourist")]
public class RegistrationsController : ControllerBase
{
    private readonly IOrganizedTripService _organizedTripService;

    public RegistrationsController(IOrganizedTripService organizedTripService)
    {
        _organizedTripService = organizedTripService;
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        [FromRoute] int organizedTripId,
        [FromBody] CreateRegistrationDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _organizedTripService.RegisterAsync(touristId, organizedTripId, request);

        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            RegistrationError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            RegistrationError.OrganizedTripNotFound => NotFound(result),
            RegistrationError.TripNotOpenForRegistration => BadRequest(result),
            RegistrationError.TripCancelled => BadRequest(result),
            RegistrationError.DeadlinePassed => BadRequest(result),
            RegistrationError.DuplicateRegistration => StatusCode(StatusCodes.Status409Conflict, result),
            RegistrationError.ExceedsCapacity => BadRequest(result),
            RegistrationError.TripFull => BadRequest(result),
            RegistrationError.InvalidNumberOfTravelers => BadRequest(result),
            _ => BadRequest(result)
        };
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyRegistrations()
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var registrations = await _organizedTripService.GetMyRegistrationsAsync(touristId);

        if (registrations.Success)
        {
            return Ok(registrations);
        }

        return registrations.Error switch
        {
            RegistrationError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, registrations),
            _ => BadRequest(registrations)
        };
    }

    [HttpPost("{registrationId:int}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(
        [FromRoute] int organizedTripId,
        [FromRoute] int registrationId)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _organizedTripService.ConfirmPaymentAsync(touristId, registrationId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            RegistrationError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            RegistrationError.RegistrationNotFound => NotFound(result),
            RegistrationError.DuplicateRegistration => StatusCode(StatusCodes.Status409Conflict, result),
            RegistrationError.TripNotOpenForRegistration => BadRequest(result),
            RegistrationError.DeadlinePassed => BadRequest(result),
            RegistrationError.ExceedsCapacity => BadRequest(result),
            RegistrationError.OrganizedTripNotFound => NotFound(result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("{registrationId:int}/cancel")]
    public async Task<IActionResult> CancelRegistration(
        [FromRoute] int organizedTripId,
        [FromRoute] int registrationId,
        [FromBody] TransitionTripRequestDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _organizedTripService.CancelRegistrationAsync(touristId, registrationId, request.CancellationReason);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            RegistrationError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            RegistrationError.RegistrationNotFound => NotFound(result),
            RegistrationError.TripNotOpenForRegistration => BadRequest(result),
            _ => BadRequest(result)
        };
    }

    private bool TryGetTouristId(out int touristId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out touristId);
    }
}