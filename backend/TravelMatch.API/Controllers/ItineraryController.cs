using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Itineraries;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Guide")]
public class ItineraryController : ControllerBase
{
    private readonly IItineraryService _itineraryService;

    public ItineraryController(IItineraryService itineraryService)
    {
        _itineraryService = itineraryService;
    }

    [HttpGet("proposals/{proposalId:int}")]
    public async Task<IActionResult> GetProposal(int proposalId)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _itineraryService.GetProposalAsync(guideId, proposalId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            ItineraryError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryError.ProposalNotFound =>
                NotFound(result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost("proposals")]
    public async Task<IActionResult> CreateProposal(
        [FromQuery] int tripRequestId,
        [FromBody] CreateProposalDto request)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                message = "Validation failed.",
                errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => e.ErrorMessage)
                            .ToArray())
            });
        }

        var result = await _itineraryService.CreateProposalAsync(
            guideId,
            tripRequestId,
            request);

        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            ItineraryError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryError.ProposalNotFound =>
                NotFound(result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpGet("proposals/{proposalId:int}/days")]
    public async Task<IActionResult> GetItinerary(int proposalId)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _itineraryService.GetItineraryAsync(guideId, proposalId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            ItineraryError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryError.ProposalNotFound =>
                NotFound(result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost("proposals/{proposalId:int}/days")]
    public async Task<IActionResult> AddDay(
        int proposalId,
        [FromBody] CreateItineraryDayDto request)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                message = "Validation failed.",
                errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => e.ErrorMessage)
                            .ToArray())
            });
        }

        var result = await _itineraryService.AddDayAsync(
            guideId,
            proposalId,
            request);

        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            ItineraryError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryError.ProposalNotFound =>
                NotFound(result),

            ItineraryError.CannotEditRejectedProposal =>
                StatusCode(StatusCodes.Status409Conflict, result),

            ItineraryError.InvalidDayNumber =>
                BadRequest(result),

            ItineraryError.TitleRequired =>
                BadRequest(result),

            ItineraryError.ActivitiesRequired =>
                BadRequest(result),

            ItineraryError.DuplicateDayNumber =>
                StatusCode(StatusCodes.Status409Conflict, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPut("days/{dayId:int}")]
    public async Task<IActionResult> UpdateDay(
        int dayId,
        [FromBody] UpdateItineraryDayDto request)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                message = "Validation failed.",
                errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => e.ErrorMessage)
                            .ToArray())
            });
        }

        var result = await _itineraryService.UpdateDayAsync(
            guideId,
            dayId,
            request);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            ItineraryError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryError.ProposalNotFound =>
                NotFound(result),

            ItineraryError.CannotEditRejectedProposal =>
                StatusCode(StatusCodes.Status409Conflict, result),

            ItineraryError.TitleRequired =>
                BadRequest(result),

            ItineraryError.ActivitiesRequired =>
                BadRequest(result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpDelete("days/{dayId:int}")]
    public async Task<IActionResult> DeleteDay(int dayId)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _itineraryService.DeleteDayAsync(guideId, dayId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            ItineraryError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryError.ProposalNotFound =>
                NotFound(result),

            ItineraryError.CannotEditRejectedProposal =>
                StatusCode(StatusCodes.Status409Conflict, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost("proposals/{proposalId:int}/save")]
    public async Task<IActionResult> SaveItinerary(
        int proposalId,
        [FromBody] IEnumerable<CreateItineraryDayDto> days)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _itineraryService.SaveItineraryAsync(
            guideId,
            proposalId,
            days);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            ItineraryError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ItineraryError.ProposalNotFound =>
                NotFound(result),

            ItineraryError.CannotEditRejectedProposal =>
                StatusCode(StatusCodes.Status409Conflict, result),

            ItineraryError.InvalidDayNumber =>
                BadRequest(result),

            ItineraryError.TitleRequired =>
                BadRequest(result),

            ItineraryError.ActivitiesRequired =>
                BadRequest(result),

            ItineraryError.DuplicateDayNumber =>
                StatusCode(StatusCodes.Status409Conflict, result),

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
