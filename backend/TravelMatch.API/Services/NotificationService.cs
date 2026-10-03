using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NotificationResponseDto>> GetByRecipientAsync(
        int recipientUserId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(notification => new NotificationResponseDto
            {
                Id = notification.Id,
                TripRequestId = notification.TripRequestId,
                Title = notification.Title,
                Message = notification.Message,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<bool> MarkAsReadAsync(int recipientUserId, int notificationId)
    {
        var notification = await _context.Notifications.SingleOrDefaultAsync(item =>
            item.Id == notificationId && item.RecipientUserId == recipientUserId);

        if (notification is null)
        {
            return false;
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return true;
    }
}