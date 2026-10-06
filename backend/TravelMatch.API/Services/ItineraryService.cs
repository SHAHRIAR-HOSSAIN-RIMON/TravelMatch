using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Itineraries;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class ItineraryService : IItineraryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ItineraryService> _logger;

    public ItineraryService(
        ApplicationDbContext context,
        ILogger<ItineraryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProposalResultDto> GetProposalAsync(
        int guideUserId,
        int proposalId)
    {
        if (proposalId <= 0)
        {
            return new ProposalResultDto
            {
                Success = false,
                Error = ItineraryError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        try
        {
            var proposal = await _context.Proposals
                .AsNoTracking()
                .Where(p => p.Id == proposalId)
                .Select(p => new ProposalDto
                {
                    Id = p.Id,
                    GuideId = p.GuideId,
                    GuideName = p.GuideId == guideUserId
                        ? "Me"
                        : "Unknown",
                    TripRequestId = p.TripRequestId,
                    ProposedPrice = p.ProposedPrice,
                    Message = p.Message,
                    Status = p.Status.ToString(),
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    ItineraryDays = p.ItineraryDays
                        .OrderBy(d => d.DayNumber)
                        .Select(d => new ItineraryDayDto
                        {
                            Id = d.Id,
                            ProposalId = d.ProposalId,
                            DayNumber = d.DayNumber,
                            Title = d.Title,
                            Activities = d.Activities,
                            Schedule = d.Schedule,
                            CreatedAt = d.CreatedAt,
                            UpdatedAt = d.UpdatedAt
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (proposal is null)
            {
                return new ProposalResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new ProposalResultDto
                {
                    Success = false,
                    Error = ItineraryError.Unauthorized,
                    Message = "You are not authorized to view this itinerary."
                };
            }

            return new ProposalResultDto
            {
                Success = true,
                Error = ItineraryError.None,
                Message = "Proposal loaded successfully.",
                Data = proposal
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load proposal {ProposalId} for guide {GuideUserId}.",
                proposalId,
                guideUserId);

            return new ProposalResultDto
            {
                Success = false,
                Error = ItineraryError.ServerError,
                Message = "Couldn't load proposal."
            };
        }
    }

    public async Task<ProposalResultDto> CreateProposalAsync(
        int guideUserId,
        int tripRequestId,
        CreateProposalDto request)
    {
        if (tripRequestId <= 0)
        {
            return new ProposalResultDto
            {
                Success = false,
                Error = ItineraryError.ProposalNotFound,
                Message = "Trip request not found."
            };
        }

        request.Message = request.Message?.Trim() ?? string.Empty;

        if (request.ProposedPrice <= 0)
        {
            return new ProposalResultDto
            {
                Success = false,
                Error = ItineraryError.Unauthorized,
                Message = "Proposed price must be greater than 0."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new ProposalResultDto
            {
                Success = false,
                Error = ItineraryError.Unauthorized,
                Message = "Message is required."
            };
        }

        try
        {
            var guideExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u =>
                    u.Id == guideUserId &&
                    u.Role == UserRole.Guide &&
                    u.IsActive);

            if (!guideExists)
            {
                return new ProposalResultDto
                {
                    Success = false,
                    Error = ItineraryError.Unauthorized,
                    Message = "Guide not found."
                };
            }

            var tripRequest = await _context.TripRequests
                .AsNoTracking()
                .Where(t => t.Id == tripRequestId)
                .Select(t => new { t.Status })
                .FirstOrDefaultAsync();

            if (tripRequest is null)
            {
                return new ProposalResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Trip request not found."
                };
            }

            if (tripRequest.Status != TripRequestStatus.Open)
            {
                return new ProposalResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "This trip request is no longer accepting proposals."
                };
            }

            var existingProposal = await _context.Proposals
                .AsNoTracking()
                .AnyAsync(p =>
                    p.GuideId == guideUserId &&
                    p.TripRequestId == tripRequestId);

            if (existingProposal)
            {
                return new ProposalResultDto
                {
                    Success = false,
                    Error = ItineraryError.Unauthorized,
                    Message = "You already have a proposal for this trip request."
                };
            }

            var proposal = new Proposal
            {
                GuideId = guideUserId,
                TripRequestId = tripRequestId,
                ProposedPrice = request.ProposedPrice,
                Message = request.Message,
                Status = ProposalStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();

            _context.Proposals.Add(proposal);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ProposalResultDto
            {
                Success = true,
                Message = "Proposal created. You can now manage the itinerary.",
                Data = new ProposalDto
                {
                    Id = proposal.Id,
                    GuideId = proposal.GuideId,
                    GuideName = "Me",
                    TripRequestId = proposal.TripRequestId,
                    ProposedPrice = proposal.ProposedPrice,
                    Message = proposal.Message,
                    Status = proposal.Status.ToString(),
                    CreatedAt = proposal.CreatedAt,
                    UpdatedAt = proposal.UpdatedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create proposal for trip request {TripRequestId} by guide {GuideUserId}.",
                tripRequestId,
                guideUserId);

            return new ProposalResultDto
            {
                Success = false,
                Error = ItineraryError.ServerError,
                Message = "Failed to create proposal. Please try again."
            };
        }
    }

    public async Task<ItineraryDayListResultDto> GetItineraryAsync(
        int guideUserId,
        int proposalId)
    {
        if (proposalId <= 0)
        {
            return new ItineraryDayListResultDto
            {
                Success = false,
                Error = ItineraryError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        try
        {
            var proposal = await _context.Proposals
                .AsNoTracking()
                .Where(p => p.Id == proposalId)
                .Select(p => new { p.GuideId, p.Status })
                .FirstOrDefaultAsync();

            if (proposal is null)
            {
                return new ItineraryDayListResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new ItineraryDayListResultDto
                {
                    Success = false,
                    Error = ItineraryError.Unauthorized,
                    Message = "You are not authorized to view this itinerary."
                };
            }

            var days = await _context.ItineraryDays
                .AsNoTracking()
                .Where(d => d.ProposalId == proposalId)
                .OrderBy(d => d.DayNumber)
                .Select(d => new ItineraryDayDto
                {
                    Id = d.Id,
                    ProposalId = d.ProposalId,
                    DayNumber = d.DayNumber,
                    Title = d.Title,
                    Activities = d.Activities,
                    Schedule = d.Schedule,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                })
                .ToListAsync();

            return new ItineraryDayListResultDto
            {
                Success = true,
                Error = ItineraryError.None,
                Message = days.Count == 0
                    ? "No itinerary days yet. Click 'Add Day' to get started."
                    : $"Showing {days.Count} day(s).",
                Data = days,
                TotalCount = days.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load itinerary for proposal {ProposalId} by guide {GuideUserId}.",
                proposalId,
                guideUserId);

            return new ItineraryDayListResultDto
            {
                Success = false,
                Error = ItineraryError.ServerError,
                Message = "Couldn't load itinerary."
            };
        }
    }

    public async Task<ItineraryResultDto> AddDayAsync(
        int guideUserId,
        int proposalId,
        CreateItineraryDayDto request)
    {
        if (proposalId <= 0)
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        request.Title = request.Title?.Trim() ?? string.Empty;
        request.Activities = request.Activities?.Trim() ?? string.Empty;
        request.Schedule = request.Schedule?.Trim();

        if (request.DayNumber <= 0)
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.InvalidDayNumber,
                Message = "Day number must be greater than 0."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.TitleRequired,
                Message = "Title is required."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Activities))
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ActivitiesRequired,
                Message = "Activities is required."
            };
        }

        try
        {
            var proposal = await _context.Proposals
                .AsNoTracking()
                .Where(p => p.Id == proposalId)
                .Select(p => new { p.GuideId, p.Status })
                .FirstOrDefaultAsync();

            if (proposal is null)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.Unauthorized,
                    Message = "You are not authorized to edit this itinerary."
                };
            }

            if (proposal.Status == ProposalStatus.Rejected)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.CannotEditRejectedProposal,
                    Message = "Cannot manage itinerary for a rejected proposal."
                };
            }

            var dayExists = await _context.ItineraryDays
                .AsNoTracking()
                .AnyAsync(d =>
                    d.ProposalId == proposalId &&
                    d.DayNumber == request.DayNumber);

            if (dayExists)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.DuplicateDayNumber,
                    Message = $"A day with number {request.DayNumber} already exists."
                };
            }

            var day = new ItineraryDay
            {
                ProposalId = proposalId,
                DayNumber = request.DayNumber,
                Title = request.Title,
                Activities = request.Activities,
                Schedule = request.Schedule,
                CreatedAt = DateTime.UtcNow
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();

            _context.ItineraryDays.Add(day);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ItineraryResultDto
            {
                Success = true,
                Message = "Day added.",
                Data = new ItineraryDayDto
                {
                    Id = day.Id,
                    ProposalId = day.ProposalId,
                    DayNumber = day.DayNumber,
                    Title = day.Title,
                    Activities = day.Activities,
                    Schedule = day.Schedule,
                    CreatedAt = day.CreatedAt,
                    UpdatedAt = day.UpdatedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to add itinerary day for proposal {ProposalId} by guide {GuideUserId}.",
                proposalId,
                guideUserId);

            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ServerError,
                Message = "Failed to save itinerary. Please try again."
            };
        }
    }

    public async Task<ItineraryResultDto> UpdateDayAsync(
        int guideUserId,
        int dayId,
        UpdateItineraryDayDto request)
    {
        if (dayId <= 0)
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ProposalNotFound,
                Message = "Itinerary day not found."
            };
        }

        request.Title = request.Title?.Trim() ?? string.Empty;
        request.Activities = request.Activities?.Trim() ?? string.Empty;
        request.Schedule = request.Schedule?.Trim();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.TitleRequired,
                Message = "Title is required."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Activities))
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ActivitiesRequired,
                Message = "Activities is required."
            };
        }

        try
        {
            var day = await _context.ItineraryDays
                .FirstOrDefaultAsync(d => d.Id == dayId);

            if (day is null)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Itinerary day not found."
                };
            }

            var proposal = await _context.Proposals
                .AsNoTracking()
                .Where(p => p.Id == day.ProposalId)
                .Select(p => new { p.GuideId, p.Status })
                .FirstOrDefaultAsync();

            if (proposal is null)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.Unauthorized,
                    Message = "You are not authorized to edit this itinerary."
                };
            }

            if (proposal.Status == ProposalStatus.Rejected)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.CannotEditRejectedProposal,
                    Message = "Cannot manage itinerary for a rejected proposal."
                };
            }

            day.Title = request.Title;
            day.Activities = request.Activities;
            day.Schedule = request.Schedule;
            day.UpdatedAt = DateTime.UtcNow;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ItineraryResultDto
            {
                Success = true,
                Message = "Day updated.",
                Data = new ItineraryDayDto
                {
                    Id = day.Id,
                    ProposalId = day.ProposalId,
                    DayNumber = day.DayNumber,
                    Title = day.Title,
                    Activities = day.Activities,
                    Schedule = day.Schedule,
                    CreatedAt = day.CreatedAt,
                    UpdatedAt = day.UpdatedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update itinerary day {DayId} by guide {GuideUserId}.",
                dayId,
                guideUserId);

            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ServerError,
                Message = "Failed to save itinerary. Please try again."
            };
        }
    }

    public async Task<ItineraryResultDto> DeleteDayAsync(
        int guideUserId,
        int dayId)
    {
        if (dayId <= 0)
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ProposalNotFound,
                Message = "Itinerary day not found."
            };
        }

        try
        {
            var day = await _context.ItineraryDays
                .AsNoTracking()
                .Where(d => d.Id == dayId)
                .Select(d => new { d.ProposalId })
                .FirstOrDefaultAsync();

            if (day is null)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Itinerary day not found."
                };
            }

            var proposal = await _context.Proposals
                .AsNoTracking()
                .Where(p => p.Id == day.ProposalId)
                .Select(p => new { p.GuideId, p.Status })
                .FirstOrDefaultAsync();

            if (proposal is null)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.Unauthorized,
                    Message = "You are not authorized to edit this itinerary."
                };
            }

            if (proposal.Status == ProposalStatus.Rejected)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.CannotEditRejectedProposal,
                    Message = "Cannot manage itinerary for a rejected proposal."
                };
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var dayToDelete = await _context.ItineraryDays
                .FirstOrDefaultAsync(d => d.Id == dayId);

            if (dayToDelete is not null)
            {
                _context.ItineraryDays.Remove(dayToDelete);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return new ItineraryResultDto
            {
                Success = true,
                Message = "Day removed from the itinerary."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete itinerary day {DayId} by guide {GuideUserId}.",
                dayId,
                guideUserId);

            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ServerError,
                Message = "Failed to remove day. Please try again."
            };
        }
    }

    public async Task<ItineraryResultDto> SaveItineraryAsync(
        int guideUserId,
        int proposalId,
        IEnumerable<CreateItineraryDayDto> days)
    {
        if (proposalId <= 0)
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        var daysList = days?.ToList() ?? new List<CreateItineraryDayDto>();

        foreach (var day in daysList)
        {
            day.Title = day.Title?.Trim() ?? string.Empty;
            day.Activities = day.Activities?.Trim() ?? string.Empty;
            day.Schedule = day.Schedule?.Trim();

            if (day.DayNumber <= 0)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.InvalidDayNumber,
                    Message = "Day number must be greater than 0."
                };
            }

            if (string.IsNullOrWhiteSpace(day.Title))
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.TitleRequired,
                    Message = $"Title is required for Day {day.DayNumber}."
                };
            }

            if (string.IsNullOrWhiteSpace(day.Activities))
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.ActivitiesRequired,
                    Message = $"Activities is required for Day {day.DayNumber}."
                };
            }
        }

        var dayNumbers = daysList
            .GroupBy(d => d.DayNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (dayNumbers.Any())
        {
            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.DuplicateDayNumber,
                Message = $"Duplicate day numbers found: {string.Join(", ", dayNumbers)}"
            };
        }

        try
        {
            var proposal = await _context.Proposals
                .AsNoTracking()
                .Where(p => p.Id == proposalId)
                .Select(p => new { p.GuideId, p.Status })
                .FirstOrDefaultAsync();

            if (proposal is null)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.Unauthorized,
                    Message = "You are not authorized to edit this itinerary."
                };
            }

            if (proposal.Status == ProposalStatus.Rejected)
            {
                return new ItineraryResultDto
                {
                    Success = false,
                    Error = ItineraryError.CannotEditRejectedProposal,
                    Message = "Cannot manage itinerary for a rejected proposal."
                };
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var existingDays = await _context.ItineraryDays
                .Where(d => d.ProposalId == proposalId)
                .ToListAsync();

            _context.ItineraryDays.RemoveRange(existingDays);
            await _context.SaveChangesAsync();

            foreach (var dayDto in daysList)
            {
                var day = new ItineraryDay
                {
                    ProposalId = proposalId,
                    DayNumber = dayDto.DayNumber,
                    Title = dayDto.Title,
                    Activities = dayDto.Activities,
                    Schedule = dayDto.Schedule,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ItineraryDays.Add(day);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ItineraryResultDto
            {
                Success = true,
                Message = "Itinerary saved."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save itinerary for proposal {ProposalId} by guide {GuideUserId}.",
                proposalId,
                guideUserId);

            return new ItineraryResultDto
            {
                Success = false,
                Error = ItineraryError.ServerError,
                Message = "Failed to save itinerary. Please try again."
            };
        }
    }
}
