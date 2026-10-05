using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NotificationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var notifications = await _context.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(notification => new
            {
                notification.Id,
                notification.Title,
                notification.Message,
                notification.CreatedAt,
                notification.IsRead
            })
            .ToListAsync();

        return Ok(notifications);
    }
}
