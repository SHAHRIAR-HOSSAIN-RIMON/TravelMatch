using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Profiles;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfilesController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyProfile()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _profileService.GetMyProfileAsync(userId);

        if (result.Success)
        {
            return Ok(result);
        }

        return ToActionResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateProfileDto request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _profileService.UpdateMyProfileAsync(userId, request);

        if (result.Success)
        {
            return Ok(result);
        }

        return ToActionResult(result);
    }

    private IActionResult ToActionResult(ProfileResultDto result)
    {
        return result.Error switch
        {
            ProfileError.UserNotFound or ProfileError.ProfileNotFound =>
                NotFound(result),

            ProfileError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    private bool TryGetUserId(out int userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(userIdClaim, out userId);
    }
}
