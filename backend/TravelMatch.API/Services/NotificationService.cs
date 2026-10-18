using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly INotificationRealtimeSender _realtimeSender;

    public NotificationService(
        ApplicationDbContext context,
        ILogger<NotificationService> logger,
        INotificationRealtimeSender realtimeSender)
    {
        _context = context;
        _logger = logger;
        _realtimeSender = realtimeSender;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(int userId)
    {
        try
        {
            return await _context.UserNotifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    RecipientUserId = n.RecipientUserId,
                    Title = n.Title,
                    Message = n.Message,
                    CreatedAt = n.CreatedAt,
                    IsRead = n.IsRead
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve notifications for user {UserId}", userId);
            throw;
        }
    }

    public async Task<NotificationResultDto> MarkAsReadAsync(int userId, int notificationId)
    {
        try
        {
            var notification = await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId);

            if (notification is null)
            {
                return new NotificationResultDto
                {
                    Success = false,
                    Error = NotificationError.NotFound,
                    Message = "Notification not found."
                };
            }

            if (notification.RecipientUserId != userId)
            {
                return new NotificationResultDto
                {
                    Success = false,
                    Error = NotificationError.Unauthorized,
                    Message = "You are not authorized to access this notification."
                };
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return new NotificationResultDto
            {
                Success = true,
                Message = "Notification marked as read.",
                Data = new NotificationDto
                {
                    Id = notification.Id,
                    RecipientUserId = notification.RecipientUserId,
                    Title = notification.Title,
                    Message = notification.Message,
                    CreatedAt = notification.CreatedAt,
                    IsRead = notification.IsRead
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification {NotificationId} as read for user {UserId}", notificationId, userId);
            return new NotificationResultDto
            {
                Success = false,
                Error = NotificationError.ServerError,
                Message = "An error occurred while marking the notification as read."
            };
        }
    }

    public async Task<NotificationDto> CreateNotificationAsync(int recipientUserId, string title, string message)
    {
        var notification = new UserNotification
        {
            RecipientUserId = recipientUserId,
            Title = title,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _context.UserNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var dto = new NotificationDto
        {
            Id = notification.Id,
            RecipientUserId = notification.RecipientUserId,
            Title = notification.Title,
            Message = notification.Message,
            CreatedAt = notification.CreatedAt,
            IsRead = notification.IsRead
        };

        // Real-time delivery is best-effort: the notification is already persisted,
        // and a hub failure must not break the caller's business flow.
        try
        {
            await _realtimeSender.SendToUserAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push notification {NotificationId} to user {UserId} in real time",
                dto.Id, dto.RecipientUserId);
        }

        return dto;
    }
}

