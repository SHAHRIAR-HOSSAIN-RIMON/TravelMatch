using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.ItineraryTracking;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class ItineraryTrackingService : IItineraryTrackingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ItineraryTrackingService> _logger;

    public ItineraryTrackingService(
        ApplicationDbContext context,
        ILogger<ItineraryTrackingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsProposalParticipantAsync(int userId, int proposalId)
    {
        var role = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => (UserRole?)user.Role)
            .FirstOrDefaultAsync();

        if (role == UserRole.Guide)
        {
            return await _context.Proposals
                .AsNoTracking()
                .AnyAsync(proposal =>
                    proposal.Id == proposalId &&
                    proposal.GuideId == userId &&
                    proposal.Status == ProposalStatus.Accepted &&
                    proposal.TripRequest.Status == TripRequestStatus.Matched &&
                    proposal.TripRequest.MatchedGuideId == userId);
        }

        if (role == UserRole.Tourist)
        {
            return await _context.Proposals
                .AsNoTracking()
                .AnyAsync(proposal =>
                    proposal.Id == proposalId &&
                    proposal.Status == ProposalStatus.Accepted &&
                    proposal.TripRequest.TouristId == userId &&
                    proposal.TripRequest.Status == TripRequestStatus.Matched &&
                    proposal.TripRequest.MatchedGuideId == proposal.GuideId);
        }

        return false;
    }

    public async Task<ItineraryTrackingListResultDto> GetItineraryTrackingAsync(
        int userId,
        int proposalId)
    {
        if (proposalId <= 0)
        {
            return new ItineraryTrackingListResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.ProposalNotFound,
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
                return new ItineraryTrackingListResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (!await IsProposalParticipantAsync(userId, proposalId))
            {
                return new ItineraryTrackingListResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.Unauthorized,
                    Message = "You are not authorized to view this itinerary."
                };
            }

            var days = await _context.ItineraryDays
                .AsNoTracking()
                .Where(d => d.ProposalId == proposalId)
                .OrderBy(d => d.DayNumber)
                .ToListAsync();

            var dayIds = days.Select(d => d.Id).ToList();

            var activities = await _context.ItineraryActivities
                .AsNoTracking()
                .Where(a => dayIds.Contains(a.ItineraryDayId))
                .OrderBy(a => a.ItineraryDayId)
                .ThenBy(a => a.OrderIndex)
                .ToListAsync();

            var activitiesByDay = activities
                .GroupBy(a => a.ItineraryDayId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var dayDtos = days.Select(d => new ItineraryDayWithActivitiesDto
            {
                Id = d.Id,
                ProposalId = d.ProposalId,
                DayNumber = d.DayNumber,
                Title = d.Title,
                Description = d.Description,
                Activities = d.Activities,
                Accommodation = d.Accommodation,
                Meals = d.Meals,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                ActivitiesList = activitiesByDay.GetValueOrDefault(d.Id, new List<ItineraryActivity>())
                    .Select(a => new ItineraryActivityDto
                    {
                        Id = a.Id,
                        ItineraryDayId = a.ItineraryDayId,
                        ProposalId = d.ProposalId,
                        OrderIndex = a.OrderIndex,
                        Title = a.Title,
                        Description = a.Description,
                        Status = GetStatusName(a.Status),
                        CreatedAt = a.CreatedAt,
                        UpdatedAt = a.UpdatedAt
                    })
                    .ToList()
            }).ToList();

            return new ItineraryTrackingListResultDto
            {
                Success = true,
                Error = ItineraryTrackingError.None,
                Message = dayDtos.Count == 0
                    ? "No itinerary days found."
                    : $"Showing {dayDtos.Count} day(s) with activities.",
                Data = dayDtos,
                TotalCount = dayDtos.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load itinerary tracking for proposal {ProposalId} by user {UserId}.",
                proposalId,
                userId);

            return new ItineraryTrackingListResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.ServerError,
                Message = "Couldn't load itinerary tracking."
            };
        }
    }

    public async Task<ItineraryTrackingResultDto> UpdateActivityStatusAsync(
        int guideUserId,
        int activityId,
        string status)
    {
        if (activityId <= 0)
        {
            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.ActivityNotFound,
                Message = "Activity not found."
            };
        }

        var activityStatus = status?.Trim().ToUpperInvariant() switch
        {
            "UPCOMING" => ActivityStatus.Upcoming,
            "IN_PROGRESS" => ActivityStatus.InProgress,
            "COMPLETED" => ActivityStatus.Completed,
            _ => (ActivityStatus?)null
        };

        if (activityStatus is null)
        {
            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.InvalidStatus,
                Message = "Invalid status. Valid values: UPCOMING, IN_PROGRESS, COMPLETED."
            };
        }

        try
        {
            var activity = await _context.ItineraryActivities
                .Include(a => a.ItineraryDay)
                .ThenInclude(d => d.Proposal)
                .FirstOrDefaultAsync(a => a.Id == activityId);

            if (activity is null)
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.ActivityNotFound,
                    Message = "Activity not found."
                };
            }

            var proposal = activity.ItineraryDay.Proposal;

            if (proposal.GuideId != guideUserId ||
                !await IsProposalParticipantAsync(guideUserId, proposal.Id))
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.Unauthorized,
                    Message = "You are not authorized to update this activity."
                };
            }

            activity.Status = activityStatus.Value;
            activity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new ItineraryTrackingResultDto
            {
                Success = true,
                Error = ItineraryTrackingError.None,
                Message = "Activity status updated.",
                Data = new ItineraryActivityDto
                {
                    Id = activity.Id,
                    ItineraryDayId = activity.ItineraryDayId,
                    ProposalId = proposal.Id,
                    OrderIndex = activity.OrderIndex,
                    Title = activity.Title,
                    Description = activity.Description,
                    Status = GetStatusName(activity.Status),
                    CreatedAt = activity.CreatedAt,
                    UpdatedAt = activity.UpdatedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update activity {ActivityId} status by guide {GuideUserId}.",
                activityId,
                guideUserId);

            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.ServerError,
                Message = "Failed to update activity status."
            };
        }
    }

    public async Task<ItineraryTrackingResultDto> CreateActivityAsync(
        int guideUserId,
        CreateItineraryActivityDto request)
    {
        if (request.ItineraryDayId <= 0)
        {
            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.DayNotFound,
                Message = "Itinerary day not found."
            };
        }

        request.Title = request.Title?.Trim() ?? string.Empty;
        request.Description = request.Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.InvalidActivity,
                Message = "Activity title is required."
            };
        }

        if (request.Title.Length > 200 || request.Description.Length > 1000)
        {
            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.InvalidActivity,
                Message = "Activity title or description exceeds the allowed length."
            };
        }

        try
        {
            var day = await _context.ItineraryDays
                .Include(d => d.Proposal)
                .FirstOrDefaultAsync(d => d.Id == request.ItineraryDayId);

            if (day is null)
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.DayNotFound,
                    Message = "Itinerary day not found."
                };
            }

            if (day.Proposal.GuideId != guideUserId)
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.Unauthorized,
                    Message = "You are not authorized to add activities to this itinerary."
                };
            }

            if (day.Proposal.Status == ProposalStatus.Rejected)
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.Unauthorized,
                    Message = "Cannot add activities to a rejected proposal."
                };
            }

            var maxOrder = await _context.ItineraryActivities
                .Where(a => a.ItineraryDayId == request.ItineraryDayId)
                .MaxAsync(a => (int?)a.OrderIndex) ?? -1;

            var orderIndex = request.OrderIndex < 0
                ? maxOrder + 1
                : request.OrderIndex;

            var orderIndexExists = await _context.ItineraryActivities
                .AnyAsync(a =>
                    a.ItineraryDayId == request.ItineraryDayId &&
                    a.OrderIndex == orderIndex);

            if (orderIndexExists)
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.DuplicateOrderIndex,
                    Message = "An activity already exists at this position."
                };
            }

            var activity = new ItineraryActivity
            {
                ItineraryDayId = request.ItineraryDayId,
                OrderIndex = orderIndex,
                Title = request.Title,
                Description = request.Description,
                Status = ActivityStatus.Upcoming,
                CreatedAt = DateTime.UtcNow
            };

            _context.ItineraryActivities.Add(activity);
            await _context.SaveChangesAsync();

            return new ItineraryTrackingResultDto
            {
                Success = true,
                Error = ItineraryTrackingError.None,
                Message = "Activity created.",
                Data = new ItineraryActivityDto
                {
                    Id = activity.Id,
                    ItineraryDayId = activity.ItineraryDayId,
                    ProposalId = day.ProposalId,
                    OrderIndex = activity.OrderIndex,
                    Title = activity.Title,
                    Description = activity.Description,
                    Status = GetStatusName(activity.Status),
                    CreatedAt = activity.CreatedAt,
                    UpdatedAt = activity.UpdatedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create activity for day {DayId} by guide {GuideUserId}.",
                request.ItineraryDayId,
                guideUserId);

            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.ServerError,
                Message = "Failed to create activity."
            };
        }
    }

    public async Task<ItineraryTrackingResultDto> DeleteActivityAsync(
        int guideUserId,
        int activityId)
    {
        if (activityId <= 0)
        {
            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.ActivityNotFound,
                Message = "Activity not found."
            };
        }

        try
        {
            var activity = await _context.ItineraryActivities
                .Include(a => a.ItineraryDay)
                .ThenInclude(d => d.Proposal)
                .FirstOrDefaultAsync(a => a.Id == activityId);

            if (activity is null)
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.ActivityNotFound,
                    Message = "Activity not found."
                };
            }

            var proposal = activity.ItineraryDay.Proposal;

            if (proposal.GuideId != guideUserId)
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.Unauthorized,
                    Message = "You are not authorized to delete this activity."
                };
            }

            if (proposal.Status == ProposalStatus.Rejected)
            {
                return new ItineraryTrackingResultDto
                {
                    Success = false,
                    Error = ItineraryTrackingError.Unauthorized,
                    Message = "Cannot delete activities from a rejected proposal."
                };
            }

            _context.ItineraryActivities.Remove(activity);
            await _context.SaveChangesAsync();

            return new ItineraryTrackingResultDto
            {
                Success = true,
                Error = ItineraryTrackingError.None,
                Message = "Activity deleted."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete activity {ActivityId} by guide {GuideUserId}.",
                activityId,
                guideUserId);

            return new ItineraryTrackingResultDto
            {
                Success = false,
                Error = ItineraryTrackingError.ServerError,
                Message = "Failed to delete activity."
            };
        }
    }

    private static string GetStatusName(ActivityStatus status)
    {
        return status switch
        {
            ActivityStatus.Upcoming => "UPCOMING",
            ActivityStatus.InProgress => "IN_PROGRESS",
            ActivityStatus.Completed => "COMPLETED",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}