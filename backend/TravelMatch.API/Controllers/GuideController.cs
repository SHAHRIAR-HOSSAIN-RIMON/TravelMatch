using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/guide")]
[Authorize(Roles = "Guide")]
public class GuideController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITripRequestService _tripRequestService;

    public GuideController(
        ApplicationDbContext context,
        ITripRequestService tripRequestService)
    {
        _context = context;
        _tripRequestService = tripRequestService;
    }

    [HttpGet("verification")]
    public async Task<IActionResult> GetVerificationStatus()
    {
        if (!TryGetUserId(out var guideId))
        {
            return Unauthorized();
        }

        var profile = await _context.GuideProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == guideId);

        if (profile is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            status = profile.VerificationStatus.ToString(),
            isVerified = profile.VerificationStatus == Models.VerificationStatus.Verified
        });
    }

    [HttpGet("triprequests")]
    public async Task<IActionResult> GetOpenTripRequests()
    {
        return Ok(await _tripRequestService.GetOpenTripRequestsAsync());
    }

    [HttpGet("triprequests/{tripRequestId:int}")]
    public async Task<IActionResult> GetOpenTripRequest(int tripRequestId)
    {
        var tripRequest = await _tripRequestService.GetOpenTripRequestAsync(tripRequestId);
        return tripRequest is null ? NotFound() : Ok(tripRequest);
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}