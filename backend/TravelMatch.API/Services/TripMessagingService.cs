using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Messaging;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class TripMessagingService : ITripMessagingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TripMessagingService> _logger;

    public TripMessagingService(
        ApplicationDbContext context,
        ILogger<TripMessagingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static readonly TripRequestStatus[] ActiveTripStatuses =
    [
        TripRequestStatus.Matched,
        TripRequestStatus.PaymentPending,
        TripRequestStatus.Planned,
        TripRequestStatus.InProgress
    ];

    private static readonly TripRequestStatus[] ReadableTripStatuses =
    [
        TripRequestStatus.Matched,
        TripRequestStatus.PaymentPending,
        TripRequestStatus.Planned,
        TripRequestStatus.InProgress,
        TripRequestStatus.Completed
    ];

    public async Task<bool> IsParticipantAsync(int userId, int proposalId, bool allowHistorical = false)
    {
        if (userId <= 0 || proposalId <= 0)
        {
            return false;
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return false;
        }

        var eligibleStatuses = allowHistorical ? ReadableTripStatuses : ActiveTripStatuses;

        if (user.Role == UserRole.Guide)
        {
            return await _context.Proposals
                .AsNoTracking()
                .AnyAsync(p =>
                    p.Id == proposalId &&
                    p.GuideId == userId &&
                    p.Status == ProposalStatus.Accepted &&
                    eligibleStatuses.Contains(p.TripRequest.Status) &&
                    p.TripRequest.MatchedGuideId == userId);
        }

        if (user.Role == UserRole.Tourist)
        {
            return await _context.Proposals
                .AsNoTracking()
                .AnyAsync(p =>
                    p.Id == proposalId &&
                    p.Status == ProposalStatus.Accepted &&
                    p.TripRequest.TouristId == userId &&
                    eligibleStatuses.Contains(p.TripRequest.Status) &&
                    p.TripRequest.MatchedGuideId == p.GuideId);
        }

        return false;
    }

    public async Task<TripMessageResultDto> SendMessageAsync(int senderUserId, SendMessageDto request)
    {
        if (request.ProposalId <= 0)
        {
            return new TripMessageResultDto
            {
                Success = false,
                Error = TripMessagingError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TripMessageResultDto
            {
                Success = false,
                Error = TripMessagingError.InvalidMessage,
                Message = "Message content cannot be empty."
            };
        }

        if (content.Length > 2000)
        {
            return new TripMessageResultDto
            {
                Success = false,
                Error = TripMessagingError.InvalidMessage,
                Message = "Message content cannot exceed 2000 characters."
            };
        }

        try
        {
            var proposalExists = await _context.Proposals
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.ProposalId);

            if (!proposalExists)
            {
                return new TripMessageResultDto
                {
                    Success = false,
                    Error = TripMessagingError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            var isAuthorized = await IsParticipantAsync(senderUserId, request.ProposalId);
            if (!isAuthorized)
            {
                return new TripMessageResultDto
                {
                    Success = false,
                    Error = TripMessagingError.Unauthorized,
                    Message = "You are not authorized to send messages in this trip."
                };
            }

            var sender = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == senderUserId);

            if (sender is null)
            {
                return new TripMessageResultDto
                {
                    Success = false,
                    Error = TripMessagingError.Unauthorized,
                    Message = "Sender user not found."
                };
            }

            var message = new TripMessage
            {
                ProposalId = request.ProposalId,
                SenderId = senderUserId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.TripMessages.Add(message);
            await _context.SaveChangesAsync();

            return new TripMessageResultDto
            {
                Success = true,
                Error = TripMessagingError.None,
                Message = "Message sent successfully.",
                Data = new TripMessageDto
                {
                    Id = message.Id,
                    ProposalId = message.ProposalId,
                    SenderId = message.SenderId,
                    SenderName = sender.FullName,
                    SenderRole = sender.Role.ToString(),
                    Content = message.Content,
                    SentAt = message.SentAt,
                    IsRead = message.IsRead
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist trip message for proposal {ProposalId} from sender {SenderId}.",
                request.ProposalId,
                senderUserId);

            return new TripMessageResultDto
            {
                Success = false,
                Error = TripMessagingError.ServerError,
                Message = "Failed to send message due to a server error."
            };
        }
    }

    public async Task<TripMessageListResultDto> GetMessagesAsync(
        int userId,
        int proposalId,
        int page = 1,
        int pageSize = 50,
        int? beforeId = null)
    {
        if (proposalId <= 0)
        {
            return new TripMessageListResultDto
            {
                Success = false,
                Error = TripMessagingError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        try
        {
            var proposalExists = await _context.Proposals
                .AsNoTracking()
                .AnyAsync(p => p.Id == proposalId);

            if (!proposalExists)
            {
                return new TripMessageListResultDto
                {
                    Success = false,
                    Error = TripMessagingError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            var isAuthorized = await IsParticipantAsync(userId, proposalId, allowHistorical: true);
            if (!isAuthorized)
            {
                return new TripMessageListResultDto
                {
                    Success = false,
                    Error = TripMessagingError.Unauthorized,
                    Message = "You are not authorized to view messages for this trip."
                };
            }

            var query = _context.TripMessages
                .AsNoTracking()
                .Where(m => m.ProposalId == proposalId);

            if (beforeId.HasValue && beforeId.Value > 0)
            {
                query = query.Where(m => m.Id < beforeId.Value);
            }

            var totalCount = await query.CountAsync();

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 1, 100);

            var messages = await query
                .Include(m => m.Sender)
                .OrderBy(m => m.SentAt)
                .ThenBy(m => m.Id)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(m => new TripMessageDto
                {
                    Id = m.Id,
                    ProposalId = m.ProposalId,
                    SenderId = m.SenderId,
                    SenderName = m.Sender.FullName,
                    SenderRole = m.Sender.Role.ToString(),
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            return new TripMessageListResultDto
            {
                Success = true,
                Error = TripMessagingError.None,
                Message = messages.Count == 0 ? "No messages found." : $"Retrieved {messages.Count} message(s).",
                Data = messages,
                TotalCount = totalCount,
                Page = safePage,
                PageSize = safePageSize,
                HasMore = (safePage * safePageSize) < totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load messages for proposal {ProposalId} by user {UserId}.",
                proposalId,
                userId);

            return new TripMessageListResultDto
            {
                Success = false,
                Error = TripMessagingError.ServerError,
                Message = "Failed to load messages due to a server error."
            };
        }
    }

    public async Task<TripMessageReadResultDto> MarkMessageAsReadAsync(int userId, int proposalId, int messageId)
    {
        if (proposalId <= 0)
        {
            return new TripMessageReadResultDto
            {
                Success = false,
                Error = TripMessagingError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        if (messageId <= 0)
        {
            return new TripMessageReadResultDto
            {
                Success = false,
                Error = TripMessagingError.MessageNotFound,
                Message = "Message not found."
            };
        }

        try
        {
            var proposalExists = await _context.Proposals
                .AsNoTracking()
                .AnyAsync(p => p.Id == proposalId);

            if (!proposalExists)
            {
                return new TripMessageReadResultDto
                {
                    Success = false,
                    Error = TripMessagingError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            var isAuthorized = await IsParticipantAsync(userId, proposalId, allowHistorical: true);
            if (!isAuthorized)
            {
                return new TripMessageReadResultDto
                {
                    Success = false,
                    Error = TripMessagingError.Unauthorized,
                    Message = "You are not authorized to access messages in this trip."
                };
            }

            // Scoped to the proposal in the same query: a message from another trip
            // is treated as not found and is never loaded or modified.
            var message = await _context.TripMessages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ProposalId == proposalId);

            if (message is null)
            {
                return new TripMessageReadResultDto
                {
                    Success = false,
                    Error = TripMessagingError.MessageNotFound,
                    Message = "Message not found."
                };
            }

            if (message.SenderId == userId)
            {
                return new TripMessageReadResultDto
                {
                    Success = false,
                    Error = TripMessagingError.InvalidMessage,
                    Message = "You cannot mark your own message as read."
                };
            }

            if (message.IsRead)
            {
                return new TripMessageReadResultDto
                {
                    Success = true,
                    Error = TripMessagingError.None,
                    Message = "Message is already marked as read.",
                    Data = new TripMessageDto
                    {
                        Id = message.Id,
                        ProposalId = message.ProposalId,
                        SenderId = message.SenderId,
                        SenderName = message.Sender.FullName,
                        SenderRole = message.Sender.Role.ToString(),
                        Content = message.Content,
                        SentAt = message.SentAt,
                        IsRead = message.IsRead
                    }
                };
            }

            message.IsRead = true;
            await _context.SaveChangesAsync();

            return new TripMessageReadResultDto
            {
                Success = true,
                Error = TripMessagingError.None,
                Message = "Message marked as read.",
                WasUnreadToRead = true,
                Data = new TripMessageDto
                {
                    Id = message.Id,
                    ProposalId = message.ProposalId,
                    SenderId = message.SenderId,
                    SenderName = message.Sender.FullName,
                    SenderRole = message.Sender.Role.ToString(),
                    Content = message.Content,
                    SentAt = message.SentAt,
                    IsRead = message.IsRead
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to mark trip message {MessageId} as read for proposal {ProposalId} by user {UserId}.",
                messageId,
                proposalId,
                userId);

            return new TripMessageReadResultDto
            {
                Success = false,
                Error = TripMessagingError.ServerError,
                Message = "Failed to mark message as read due to a server error."
            };
        }
    }
}

