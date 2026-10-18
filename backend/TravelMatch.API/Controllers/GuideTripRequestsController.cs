using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Proposals;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/trip-requests")]
[Authorize(Roles = "Guide")]
public class GuideTripRequestsController : ControllerBase
{
    private readonly IGuideTripRequestService _guideTripRequestService;
    private readonly IProposalService _proposalService;

    public GuideTripRequestsController(
        IGuideTripRequestService guideTripRequestService,
        IProposalService proposalService)
    {
        _guideTripRequestService = guideTripRequestService;
        _proposalService = proposalService;
    }

    // ─── Browse open requests ────────────────────────────────────────────────

    [HttpGet("open")]
    public async Task<IActionResult> GetOpenTripRequests(
        [FromQuery] OpenTripRequestQueryDto query)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _guideTripRequestService.GetOpenTripRequestsAsync(guideId, query);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            OpenTripRequestError.GuideNotFound =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            OpenTripRequestError.InvalidFilter =>
                BadRequest(result),

            OpenTripRequestError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ => StatusCode(StatusCodes.Status404NotFound, result)
        };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOpenTripRequestDetail(int id)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _guideTripRequestService.GetOpenTripRequestDetailAsync(guideId, id);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            OpenTripRequestError.GuideNotFound =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            OpenTripRequestError.TripRequestNotFound =>
                NotFound(result),

            OpenTripRequestError.TripRequestNotAvailable =>
                StatusCode(StatusCodes.Status409Conflict, result),

            OpenTripRequestError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ => BadRequest(result)
        };
    }

    // ─── Guide proposal actions ──────────────────────────────────────────────

    /// <summary>
    /// Submit a Draft proposal (transition Draft → Submitted).
    /// The proposal must first be created via POST /api/itinerary/proposals.
    /// This step finalises availability, inclusions, exclusions, and notes.
    /// </summary>
    [HttpPost("{tripRequestId:int}/proposals/{proposalId:int}/submit")]
    public async Task<IActionResult> SubmitProposal(
        [FromRoute] int tripRequestId,
        [FromRoute] int proposalId,
        [FromBody] SubmitProposalDto request)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
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
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
            });
        }

        var result = await _proposalService.SubmitProposalAsync(
            guideId, tripRequestId, proposalId, request);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            SubmitProposalError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            SubmitProposalError.GuideNotFound =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            SubmitProposalError.GuideNotVerified =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            SubmitProposalError.ProposalNotFound or
            SubmitProposalError.TripRequestNotFound =>
                NotFound(result),

            SubmitProposalError.ProposalNotInDraftState or
            SubmitProposalError.TripRequestNotAcceptingProposals or
            SubmitProposalError.DuplicateProposal =>
                StatusCode(StatusCodes.Status409Conflict, result),

            SubmitProposalError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Update an editable (Draft or Submitted) proposal.
    /// Send only the fields you want to change.
    /// </summary>
    [HttpPut("{tripRequestId:int}/proposals/{proposalId:int}")]
    public async Task<IActionResult> UpdateProposal(
        [FromRoute] int tripRequestId,
        [FromRoute] int proposalId,
        [FromBody] UpdateProposalDto request)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _proposalService.UpdateProposalAsync(
            guideId, tripRequestId, proposalId, request);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            UpdateProposalError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            UpdateProposalError.ProposalNotFound =>
                NotFound(result),

            UpdateProposalError.ProposalNotEditable =>
                StatusCode(StatusCodes.Status409Conflict, result),

            UpdateProposalError.ValidationError =>
                BadRequest(result),

            UpdateProposalError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Withdraw a proposal (Draft or Submitted → Withdrawn).
    /// Cannot withdraw an already accepted or rejected proposal.
    /// </summary>
    [HttpPost("{tripRequestId:int}/proposals/{proposalId:int}/withdraw")]
    public async Task<IActionResult> WithdrawProposal(
        [FromRoute] int tripRequestId,
        [FromRoute] int proposalId)
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _proposalService.WithdrawProposalAsync(
            guideId, tripRequestId, proposalId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            WithdrawProposalError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            WithdrawProposalError.ProposalNotFound =>
                NotFound(result),

            WithdrawProposalError.ProposalAlreadyWithdrawn or
            WithdrawProposalError.ProposalAlreadyAccepted or
            WithdrawProposalError.ProposalAlreadyRejected =>
                StatusCode(StatusCodes.Status409Conflict, result),

            WithdrawProposalError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// List all proposals submitted by the authenticated guide across all trip requests.
    /// </summary>
    [HttpGet("proposals/mine")]
    public async Task<IActionResult> GetMyProposals()
    {
        if (!TryGetGuideId(out var guideId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _proposalService.GetMyProposalsAsync(guideId);

        if (result.Success)
        {
            return Ok(result);
        }

        return StatusCode(StatusCodes.Status500InternalServerError, result);
    }

    private bool TryGetGuideId(out int guideId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out guideId);
    }
}