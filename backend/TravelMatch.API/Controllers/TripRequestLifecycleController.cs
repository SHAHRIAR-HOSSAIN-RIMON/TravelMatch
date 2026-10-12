using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/trip-requests/{tripRequestId:int}/lifecycle")]
[Authorize(Roles = "Tourist")]
public class TripRequestLifecycleController : ControllerBase
{
    private readonly ITripRequestService _tripRequestService;

    public TripRequestLifecycleController(ITripRequestService tripRequestService)
    {
        _tripRequestService = tripRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLifecycle([FromRoute] int tripRequestId)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.GetLifecycleAsync(touristId, tripRequestId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripLifecycleError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            TripLifecycleError.TripRequestNotFound => NotFound(result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("proposals-received")]
    public async Task<IActionResult> TransitionToProposalsReceived([FromRoute] int tripRequestId)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.TransitionToProposalsReceivedAsync(touristId, tripRequestId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripLifecycleError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            TripLifecycleError.TripRequestNotFound => NotFound(result),
            TripLifecycleError.InvalidStatusTransition => StatusCode(StatusCodes.Status409Conflict, result),
            TripLifecycleError.NoValidProposals => BadRequest(result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("matched")]
    public async Task<IActionResult> TransitionToMatched(
        [FromRoute] int tripRequestId,
        [FromBody] TransitionTripRequestDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (request.ProposalId <= 0)
        {
            return BadRequest(new { message = "Proposal ID is required." });
        }

        var result = await _tripRequestService.TransitionToMatchedAsync(touristId, tripRequestId, request.ProposalId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripLifecycleError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            TripLifecycleError.TripRequestNotFound => NotFound(result),
            TripLifecycleError.ProposalNotFound => NotFound(result),
            TripLifecycleError.ProposalNotSubmitted => BadRequest(result),
            TripLifecycleError.InvalidStatusTransition => StatusCode(StatusCodes.Status409Conflict, result),
            TripLifecycleError.AlreadyMatched => StatusCode(StatusCodes.Status409Conflict, result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("payment-pending")]
    public async Task<IActionResult> TransitionToPaymentPending(
        [FromRoute] int tripRequestId,
        [FromBody] TransitionTripRequestDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (request.AcceptedPrice <= 0)
        {
            return BadRequest(new { message = "Accepted price is required and must be greater than zero." });
        }

        var result = await _tripRequestService.TransitionToPaymentPendingAsync(touristId, tripRequestId, request.AcceptedPrice);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripLifecycleError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            TripLifecycleError.TripRequestNotFound => NotFound(result),
            TripLifecycleError.InvalidStatusTransition => StatusCode(StatusCodes.Status409Conflict, result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("planned")]
    public async Task<IActionResult> TransitionToPlanned([FromRoute] int tripRequestId)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.TransitionToPlannedAsync(touristId, tripRequestId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripLifecycleError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            TripLifecycleError.TripRequestNotFound => NotFound(result),
            TripLifecycleError.InvalidStatusTransition => StatusCode(StatusCodes.Status409Conflict, result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("in-progress")]
    public async Task<IActionResult> TransitionToInProgress([FromRoute] int tripRequestId)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.TransitionToInProgressAsync(touristId, tripRequestId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripLifecycleError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            TripLifecycleError.TripRequestNotFound => NotFound(result),
            TripLifecycleError.InvalidStatusTransition => StatusCode(StatusCodes.Status409Conflict, result),
            TripLifecycleError.TripAlreadyStarted => StatusCode(StatusCodes.Status409Conflict, result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("completed")]
    public async Task<IActionResult> TransitionToCompleted([FromRoute] int tripRequestId)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.TransitionToCompletedAsync(touristId, tripRequestId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripLifecycleError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            TripLifecycleError.TripRequestNotFound => NotFound(result),
            TripLifecycleError.InvalidStatusTransition => StatusCode(StatusCodes.Status409Conflict, result),
            TripLifecycleError.TripAlreadyCompleted => StatusCode(StatusCodes.Status409Conflict, result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("cancelled")]
    public async Task<IActionResult> TransitionToCancelled(
        [FromRoute] int tripRequestId,
        [FromBody] TransitionTripRequestDto request)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _tripRequestService.TransitionToCancelledAsync(touristId, tripRequestId, request.CancellationReason);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripLifecycleError.Unauthorized => StatusCode(StatusCodes.Status403Forbidden, result),
            TripLifecycleError.TripRequestNotFound => NotFound(result),
            TripLifecycleError.InvalidStatusTransition => StatusCode(StatusCodes.Status409Conflict, result),
            TripLifecycleError.TripAlreadyCancelled => StatusCode(StatusCodes.Status409Conflict, result),
            _ => BadRequest(result)
        };
    }

    private bool TryGetTouristId(out int touristId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out touristId);
    }
}