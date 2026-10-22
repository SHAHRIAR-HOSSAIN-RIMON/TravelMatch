using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly IDisputeService _disputeService;

    public DisputesController(IDisputeService disputeService)
    {
        _disputeService = disputeService;
    }

    [HttpPost]
    public async Task<IActionResult> RaiseDispute([FromBody] RaiseDisputeDto request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        if (!Enum.TryParse<UserRole>(User.FindFirstValue(ClaimTypes.Role), out var userRole))
            return Unauthorized();

        var result = await _disputeService.RaiseDisputeAsync(userId, userRole, request);

        if (result.Success)
            return StatusCode(201, result);

        return result.Error switch
        {
            DisputeError.TripNotFound  => StatusCode(404, result),
            DisputeError.NotAuthorized => StatusCode(403, result),
            _                          => BadRequest(result)
        };
    }

    /// <summary>
    /// Administrator-only listing of all disputes, optionally filtered by status.
    /// Returns disputes ordered oldest-first to support the admin review queue.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDisputes([FromQuery] string? status)
    {
        DisputeStatus? parsed = null;

        if (status is not null)
        {
            if (!Enum.TryParse<DisputeStatus>(status, ignoreCase: true, out var statusValue))
                return BadRequest(new DisputeListResultDto
                {
                    Success = false,
                    Error = DisputeError.InvalidInput,
                    Message = $"'{status}' is not a valid dispute status. Valid values: {string.Join(", ", Enum.GetNames<DisputeStatus>())}."
                });

            parsed = statusValue;
        }

        var result = await _disputeService.GetDisputesAsync(parsed);
        return Ok(result);
    }

    /// <summary>
    /// Administrator-only retrieval of a single dispute by ID.
    /// </summary>
    [HttpGet("{disputeId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDisputeById([FromRoute] int disputeId)
    {
        var result = await _disputeService.GetDisputeByIdAsync(disputeId);

        if (!result.Success && result.Error == DisputeError.DisputeNotFound)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Administrator-only resolution of an open dispute. Resolving releases the escrow
    /// block the dispute placed on the associated trip's payments.
    /// </summary>
    [HttpPost("{disputeId:int}/resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResolveDispute([FromRoute] int disputeId, [FromBody] ResolveDisputeDto request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
            return Unauthorized();

        var result = await _disputeService.ResolveDisputeAsync(
            adminUserId, disputeId, request?.ResolutionNote);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            DisputeError.DisputeNotFound       => StatusCode(404, result),
            DisputeError.AdminOnly             => StatusCode(403, result),
            DisputeError.DisputeAlreadyResolved => StatusCode(409, result),
            _                                  => BadRequest(result)
        };
    }
}
