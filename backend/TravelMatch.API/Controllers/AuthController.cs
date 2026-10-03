using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Auth;
using TravelMatch.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            return result.Error switch
            {
                RegisterError.DuplicateEmail => Conflict(result),
                RegisterError.InvalidRole => BadRequest(result),
                _ => BadRequest(result)
            };
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);

        if (!result.Success)
        {
            return result.Error switch
            {
                LoginError.InvalidCredentials => Unauthorized(result.Message),
                LoginError.AccountInactive => StatusCode(403, result.Message),
                _ => BadRequest(result.Message)
            };
        }

        return Ok(result.Data);
    }
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetCurrentUserAsync(userId);

    if (user is null)
    {
        return NotFound();
    }

    return Ok(user);
}
}
