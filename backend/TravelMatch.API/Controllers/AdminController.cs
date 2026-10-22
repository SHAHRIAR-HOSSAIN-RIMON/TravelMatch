using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Admin;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Controllers;

/// <summary>
/// Admin-only dashboard and user-management endpoints.
/// The service layer independently re-validates the admin role.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>Returns aggregate platform counts for the dashboard.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        if (!TryGetAdminId(out var adminId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _adminService.GetDashboardAsync(adminId);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            AdminError.AdminOnly => StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }

    /// <summary>Returns a paged user list, optionally filtered by role and active status.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        if (!TryGetAdminId(out var adminId))
            return Unauthorized(new { message = "Invalid authentication token." });

        UserRole? roleFilter = null;

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (int.TryParse(role, out _)
                || !Enum.TryParse<UserRole>(role.Trim(), ignoreCase: true, out var parsedRole)
                || !Enum.IsDefined(parsedRole))
                return BadRequest(new AdminUserListResultDto
                {
                    Success = false,
                    Error = AdminError.InvalidInput,
                    Message = $"'{role}' is not a valid role. Valid values: {string.Join(", ", Enum.GetNames<UserRole>())}."
                });

            roleFilter = parsedRole;
        }

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            return BadRequest(new AdminUserListResultDto
            {
                Success = false,
                Error = AdminError.InvalidInput,
                Message = $"page must be at least 1 and pageSize must be between 1 and {MaxPageSize}."
            });

        var result = await _adminService.GetUsersAsync(adminId, roleFilter, isActive, page, pageSize);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            AdminError.AdminOnly => StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }

    /// <summary>Activates or deactivates a user. Roles are never changed here.</summary>
    [HttpPatch("users/{userId:int}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        [FromRoute] int userId,
        [FromBody] UpdateUserStatusDto request)
    {
        if (!TryGetAdminId(out var adminId))
            return Unauthorized(new { message = "Invalid authentication token." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request is null || request.IsActive is null)
            return BadRequest(new AdminUserStatusResultDto
            {
                Success = false,
                Error = AdminError.InvalidInput,
                Message = "The 'isActive' field is required."
            });

        var result = await _adminService.SetUserStatusAsync(adminId, userId, request.IsActive.Value);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            AdminError.AdminOnly => StatusCode(StatusCodes.Status403Forbidden, result),
            AdminError.UserNotFound => NotFound(result),
            AdminError.CannotDeactivateSelf => BadRequest(result),
            _ => BadRequest(result)
        };
    }

    private bool TryGetAdminId(out int adminId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out adminId);
    }
}
