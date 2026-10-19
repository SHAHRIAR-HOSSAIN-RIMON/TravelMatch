using Microsoft.AspNetCore.SignalR;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.Hubs;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Services;

public class SignalRNotificationSender : INotificationRealtimeSender
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationSender(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendToUserAsync(NotificationDto notification, CancellationToken cancellationToken = default)
    {
        // RecipientUserId comes from the persisted notification, so a client can never
        // steer delivery to another user's private group.
        var groupName = NotificationHub.GroupName(notification.RecipientUserId);
        return _hubContext.Clients.Group(groupName)
            .SendAsync("ReceiveNotification", notification, cancellationToken);
    }
}
