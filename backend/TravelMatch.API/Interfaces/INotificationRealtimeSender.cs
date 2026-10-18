using TravelMatch.API.DTOs.Notifications;

namespace TravelMatch.API.Interfaces;

public interface INotificationRealtimeSender
{
    Task SendToUserAsync(NotificationDto notification, CancellationToken cancellationToken = default);
}
