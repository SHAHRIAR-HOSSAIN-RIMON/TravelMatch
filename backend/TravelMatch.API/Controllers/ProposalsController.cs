using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Proposals;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/trip-requests/{tripRequestId:int}/proposals")]
[Authorize(Roles = "Tourist")]
public class ProposalsController : ControllerBase
{
    private readonly IProposalService _proposalService;

    public ProposalsController(IProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProposals(
        [FromRoute] int tripRequestId,
        [FromQuery] ProposalSort sort = ProposalSort.Newest)
    {
        if (!Enum.IsDefined(sort))
        {
            return BadRequest(new
            {
                message = "Invalid proposal sort option."
            });
        }

        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _proposalService.GetProposalsAsync(
            touristId,
            tripRequestId,
            sort);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            ProposalError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ProposalError.TripRequestNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            ProposalError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                Ok(result)
        };
    }

    [HttpGet("{proposalId:int}")]
    public async Task<IActionResult> GetProposalDetail(
        [FromRoute] int tripRequestId,
        [FromRoute] int proposalId)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _proposalService.GetProposalDetailAsync(
            touristId,
            tripRequestId,
            proposalId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            ProposalError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            ProposalError.TripRequestNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            ProposalError.ProposalNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            ProposalError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                StatusCode(StatusCodes.Status404NotFound, result)
        };
    }

    [HttpPost("{proposalId:int}/accept")]
    public async Task<IActionResult> AcceptProposal(
        [FromRoute] int tripRequestId,
        [FromRoute] int proposalId)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _proposalService.AcceptProposalAsync(
            touristId,
            tripRequestId,
            proposalId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            AcceptProposalError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            AcceptProposalError.TripRequestNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            AcceptProposalError.ProposalNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            AcceptProposalError.AlreadyAccepted =>
                StatusCode(StatusCodes.Status409Conflict, result),

            AcceptProposalError.ProposalNotAvailable =>
                StatusCode(StatusCodes.Status409Conflict, result),

            AcceptProposalError.TripRequestAlreadyMatched =>
                StatusCode(StatusCodes.Status409Conflict, result),

            AcceptProposalError.TripRequestNotAvailable =>
                StatusCode(StatusCodes.Status409Conflict, result),

            AcceptProposalError.ServerError =>
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
