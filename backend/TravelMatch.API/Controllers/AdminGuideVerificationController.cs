using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.GuideVerification;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

/// <summary>
/// Admin-only endpoints for managing guide verification status.
/// All routes require an authenticated user with the Admin role.
/// The service layer independently re-validates the admin role so
/// that programmatic callers cannot bypass the JWT check.
/// </summary>
[ApiController]
[Route("api/admin/guide-verification")]
[Authorize(Roles = "Admin")]
public class AdminGuideVerificationController : ControllerBase
{
    private readonly IGuideVerificationService _verificationService;

    public AdminGuideVerificationController(IGuideVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    /// <summary>
    /// Returns a paged list of guide profiles whose VerificationStatus is Pending.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingGuides(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetAdminId(out var adminId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _verificationService.GetPendingGuidesAsync(adminId, page, pageSize);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            GuideVerificationError.AdminOnly => StatusCode(StatusCodes.Status403Forbidden, result),
            _                               => BadRequest(result)
        };
    }

    /// <summary>
    /// Sets a guide's VerificationStatus to Verified or Rejected.
    /// A note is required when rejecting.
    /// </summary>
    [HttpPatch("{guideUserId:int}")]
    public async Task<IActionResult> SetVerificationStatus(
        [FromRoute] int guideUserId,
        [FromBody] SetVerificationStatusDto request)
    {
        if (!TryGetAdminId(out var adminId))
            return Unauthorized(new { message = "Invalid authentication token." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _verificationService.SetVerificationStatusAsync(
            adminId, guideUserId, request.Status, request.Note);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            GuideVerificationError.AdminOnly    => StatusCode(StatusCodes.Status403Forbidden, result),
            GuideVerificationError.GuideNotFound => NotFound(result),
            GuideVerificationError.InvalidStatus => BadRequest(result),
            GuideVerificationError.InvalidInput  => BadRequest(result),
            _                                   => BadRequest(result)
        };
    }

    private bool TryGetAdminId(out int adminId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out adminId);
    }
}
