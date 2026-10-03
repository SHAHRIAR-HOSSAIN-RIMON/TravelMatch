using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Proposals;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Guide")]
public class ProposalsController : ControllerBase
{
    private readonly ITripRequestService _tripRequestService;

    public ProposalsController(ITripRequestService tripRequestService)
    {
        _tripRequestService = tripRequestService;
    }

    [HttpPost("~/api/triprequests/{tripRequestId:int}/proposals")]
    public async Task<IActionResult> Create(
        int tripRequestId,
        [FromBody] CreateProposalDto request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _tripRequestService.CreateProposalAsync(
            guideId,
            tripRequestId,
            request);

        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            ProposalError.GuideNotVerified => StatusCode(
                StatusCodes.Status403Forbidden,
                result),
            ProposalError.TripRequestNotFound => NotFound(result),
            ProposalError.TripRequestNotOpen or ProposalError.DuplicateProposal =>
                Conflict(result),
            _ => BadRequest(result)
        };
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var guideId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var proposals = await _tripRequestService.GetProposalsByGuideAsync(guideId);
        return Ok(proposals);
    }
}