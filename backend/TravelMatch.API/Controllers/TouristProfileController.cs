using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Tourist;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/tourist/profile")]
[Authorize(Roles = "Tourist")]
public class TouristProfileController : ControllerBase
{
    private readonly ITouristProfileService _touristProfileService;

    public TouristProfileController(ITouristProfileService touristProfileService)
    {
        _touristProfileService = touristProfileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = GetAuthenticatedUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var profile = await _touristProfileService.GetMyProfileAsync(userId.Value);

        if (profile is null)
        {
            return NotFound(new
            {
                message = "Tourist profile was not found."
            });
        }

        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateTouristProfileDto request)
    {
        var userId = GetAuthenticatedUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _touristProfileService.UpdateMyProfileAsync(
            userId.Value,
            request);

        if (!result.Success)
        {
            if (result.Error == "Email is already registered by another user.")
            {
                return Conflict(new
                {
                    message = result.Error
                });
            }

            return NotFound(new
            {
                message = result.Error
            });
        }

        return Ok(result.Data);
    }

    private int? GetAuthenticatedUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
