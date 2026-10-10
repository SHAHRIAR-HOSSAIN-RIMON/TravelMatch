using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.GuideApplications;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Organizer")]
public class OrganizerApplicationsController : ControllerBase
{
    private readonly IGuideApplicationService _guideApplicationService;

    public OrganizerApplicationsController(
        IGuideApplicationService guideApplicationService)
    {
        _guideApplicationService = guideApplicationService;
    }

    [HttpGet("trips/{tripId:int}/applications")]
    public async Task<IActionResult> GetTripApplications(
        int tripId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var organizerId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _guideApplicationService
            .GetTripApplicationsAsync(organizerId, tripId, page, pageSize);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            GuideApplicationError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            GuideApplicationError.TripNotFound =>
                NotFound(result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost("trips/{tripId:int}/applications/{applicationId:int}/select")]
    public async Task<IActionResult> SelectGuide(
        int tripId,
        int applicationId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var organizerId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _guideApplicationService
            .SelectGuideApplicationAsync(organizerId, tripId, applicationId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            GuideApplicationError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            GuideApplicationError.OrganizerNotFound =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            GuideApplicationError.TripNotFound =>
                NotFound(result),

            GuideApplicationError.ApplicationNotFound =>
                NotFound(result),

            GuideApplicationError.GuideNotVerified =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            GuideApplicationError.TripNotInGuideSelection =>
                StatusCode(StatusCodes.Status409Conflict, result),

            GuideApplicationError.GuideAlreadySelected =>
                StatusCode(StatusCodes.Status409Conflict, result),

            GuideApplicationError.ApplicationNotPending =>
                StatusCode(StatusCodes.Status409Conflict, result),

            GuideApplicationError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }
}