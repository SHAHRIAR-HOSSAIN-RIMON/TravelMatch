using TravelMatch.API.DTOs.Notifications;

namespace TravelMatch.API.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponseDto>> GetByRecipientAsync(int recipientUserId);

    Task<bool> MarkAsReadAsync(int recipientUserId, int notificationId);
}