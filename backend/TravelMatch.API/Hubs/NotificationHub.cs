using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TravelMatch.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId == 0)
        {
            _logger.LogWarning("Notification hub connection without a valid authenticated user id");
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            Context.Abort();
            return;
        }

        // The group is derived exclusively from the authenticated principal, never from client input.
        var groupName = GroupName(userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} connected to notifications group {GroupName}", userId, groupName);

        await Clients.Caller.SendAsync("JoinedNotificationsGroup", groupName);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} disconnected from notifications hub", userId);
        await base.OnDisconnectedAsync(exception);
    }

    public static string GroupName(int userId) => $"user-{userId}";

    private int GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }
}
