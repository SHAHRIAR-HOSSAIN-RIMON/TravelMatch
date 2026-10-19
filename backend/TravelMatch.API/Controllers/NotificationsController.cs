using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var notifications = await _notificationService.GetMyNotificationsAsync(userId);
        return Ok(notifications);
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> MarkAsRead(int notificationId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _notificationService.MarkAsReadAsync(userId, notificationId);

        if (!result.Success)
        {
            return result.Error switch
            {
                NotificationError.NotFound => NotFound(new { message = result.Message }),
                NotificationError.Unauthorized => Forbid(),
                _ => StatusCode(500, new { message = result.Message })
            };
        }

        return Ok(result.Data);
    }
}
