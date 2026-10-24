using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Profile;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

/// <summary>
/// Self-service profile management. The profile identity always comes from the
/// validated JWT; no user ID is accepted from the request body or route.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _profileService.GetMyProfileAsync(userId);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            ProfileError.UserNotFound => NotFound(result),
            ProfileError.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequestDto request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _profileService.UpdateMyProfileAsync(userId, request);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            ProfileError.UserNotFound or ProfileError.ProfileNotFound => NotFound(result),
            ProfileError.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}
