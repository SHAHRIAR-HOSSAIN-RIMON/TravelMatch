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
}
