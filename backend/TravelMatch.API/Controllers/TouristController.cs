
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Tourist;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Tourist")]
public class TouristController : ControllerBase
{
    private readonly ITouristService _touristService;

    public TouristController(ITouristService touristService)
    {
        _touristService = touristService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetAuthenticatedUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _touristService.GetProfileAsync(userId.Value);

        if (result.Success)
        {
            return Ok(result);
        }

        return ToStatusCode(result);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateTouristProfileRequestDto request)
    {
        var userId = GetAuthenticatedUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _touristService.UpdateProfileAsync(
            userId.Value,
            request);

        if (result.Success)
        {
            return Ok(result);
        }

        return ToStatusCode(result);
    }

    private int? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return userId;
    }

    private IActionResult ToStatusCode(TouristProfileResultDto result)
    {
        return result.Error switch
        {
            TouristProfileError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TouristProfileError.AccountInactive =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TouristProfileError.ProfileNotFound =>
                StatusCode(StatusCodes.Status404NotFound, result),

            _ =>
                BadRequest(result)
        };
    }
}
