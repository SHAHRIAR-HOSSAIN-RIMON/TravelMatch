using TravelMatch.API.DTOs.Notifications;

namespace TravelMatch.API.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(int userId);

    Task<NotificationResultDto> MarkAsReadAsync(int userId, int notificationId);

    Task<NotificationDto> CreateNotificationAsync(int recipientUserId, string title, string message);
}

