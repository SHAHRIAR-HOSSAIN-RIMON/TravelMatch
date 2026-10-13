using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;
using TravelMatch.API.Services;

namespace TravelMatch.API.Services;

public class TripRequestService : ITripRequestService
{
    private readonly ApplicationDbContext _context;

    public TripRequestService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TripRequestResultDto> CreateAsync(
        int touristId,
        CreateTripRequestDto request)
    {
        
        if (request.StartDate == DateOnly.MinValue)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.InvalidStartDate,
                Message = "Start date is required."
            };
        }

        if (request.EndDate == DateOnly.MinValue)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.InvalidEndDate,
                Message = "End date is required."
            };
        }

        var bangladeshTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

        var bangladeshToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                bangladeshTimeZone));

        if (request.StartDate < bangladeshToday)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.StartDateInPast,
                Message = "Start date cannot be in the past."
            };
        }

        if (request.EndDate < request.StartDate)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.EndDateBeforeStartDate,
                Message = "End date cannot be before start date."
            };
        }

        if (request.NumberOfTravelers < 1)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.InvalidNumberOfTravelers,
                Message = "Number of travelers must be at least 1."
            };
        }

        if (request.Budget <= 0)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.InvalidBudget,
                Message = "Budget must be greater than zero."
            };
        }

        var budgetMin = request.BudgetMin ?? request.Budget;
        var budgetMax = request.BudgetMax ?? request.Budget;

        if (request.BudgetMin.HasValue != request.BudgetMax.HasValue ||
            budgetMin > budgetMax)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.InvalidBudget,
                Message = "Provide both budget limits and ensure the minimum does not exceed the maximum."
            };
        }

        var touristExists = await _context.Users.AnyAsync(u =>
            u.Id == touristId &&
            u.Role == UserRole.Tourist &&
            u.IsActive);

        if (!touristExists)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.Unauthorized,
                Message = "Only active tourist accounts can create trip requests."
            };
        }

        var tripRequest = new TripRequest
        {
            TouristId = touristId,
            Destination = request.Destination.Trim(),
            TripType = request.TripType.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfTravelers = request.NumberOfTravelers,
            Budget = request.Budget,
            BudgetMin = budgetMin,
            BudgetMax = budgetMax,
            Description = request.Description?.Trim() ?? string.Empty,
            TravelPreferences = request.TravelPreferences?.Trim() ?? string.Empty,
            Status = TripRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.TripRequests.Add(tripRequest);

        await _context.SaveChangesAsync();

        return new TripRequestResultDto
        {
            Success = true,
            Message = "Trip request created successfully.",
            Data = ToResponse(tripRequest)
        };
    }

    public async Task<TripRequestResultDto> CancelAsync(
        int touristId,
        int tripRequestId)
    {
        var tripRequest = await _context.TripRequests
            .FirstOrDefaultAsync(t => t.Id == tripRequestId);

        if (tripRequest is null)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.TripRequestNotFound,
                Message = "Trip request was not found."
            };
        }

        if (tripRequest.TouristId != touristId)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.NotTripRequestOwner,
                Message = "You are not allowed to cancel this trip request."
            };
        }

        if (tripRequest.Status == TripRequestStatus.Cancelled)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.AlreadyCancelled,
                Message = "Trip request is already cancelled."
            };
        }

        if (tripRequest.Status == TripRequestStatus.Completed)
        {
            return new TripRequestResultDto
            {
                Success = false,
                Error = TripRequestError.TripRequestCompleted,
                Message = "A completed trip request cannot be cancelled."
            };
        }

        tripRequest.Status = TripRequestStatus.Cancelled;
        tripRequest.CancelledAt = DateTime.UtcNow;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TripRequestResultDto
        {
            Success = true,
            Message = "Trip request cancelled successfully.",
            Data = ToResponse(tripRequest)
        };
    }

    public async Task<TripRequestLifecycleResultDto> GetLifecycleAsync(int touristId, int tripRequestId)
    {
        var tripRequest = await _context.TripRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tripRequestId && t.TouristId == touristId);

        if (tripRequest == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        return new TripRequestLifecycleResultDto
        {
            Success = true,
            Message = "Trip request lifecycle retrieved successfully.",
            Data = ToLifecycleDto(tripRequest)
        };
    }

    public async Task<TripRequestLifecycleResultDto> TransitionToProposalsReceivedAsync(int touristId, int tripRequestId)
    {
        var tripRequest = await _context.TripRequests
            .FirstOrDefaultAsync(t => t.Id == tripRequestId && t.TouristId == touristId);

        if (tripRequest == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        var hasValidProposal = await _context.Proposals.AnyAsync(p =>
            p.TripRequestId == tripRequestId && p.Status == ProposalStatus.Submitted);

        if (!hasValidProposal)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.NoValidProposals,
                Message = "Cannot transition: no valid proposals received."
            };
        }

        var transitionError = TripStateMachine.GetTransitionError(tripRequest.Status, TripRequestStatus.ProposalsReceived);
        if (!string.IsNullOrEmpty(transitionError))
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.InvalidStatusTransition,
                Message = transitionError
            };
        }

        tripRequest.Status = TripRequestStatus.ProposalsReceived;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TripRequestLifecycleResultDto
        {
            Success = true,
            Message = "Trip request status updated to PROPOSALS_RECEIVED.",
            Data = ToLifecycleDto(tripRequest)
        };
    }

    public async Task<TripRequestLifecycleResultDto> TransitionToMatchedAsync(int touristId, int tripRequestId, int proposalId)
    {
        var tripRequest = await _context.TripRequests
            .FirstOrDefaultAsync(t => t.Id == tripRequestId && t.TouristId == touristId);

        if (tripRequest == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        var proposal = await _context.Proposals
            .FirstOrDefaultAsync(p => p.Id == proposalId && p.TripRequestId == tripRequestId);

        if (proposal == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        if (proposal.Status != ProposalStatus.Submitted)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.ProposalNotSubmitted,
                Message = "Proposal is not in submitted status."
            };
        }

        var transitionError = TripStateMachine.GetTransitionError(tripRequest.Status, TripRequestStatus.Matched);
        if (!string.IsNullOrEmpty(transitionError))
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.InvalidStatusTransition,
                Message = transitionError
            };
        }

        if (tripRequest.Status == TripRequestStatus.Matched)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.AlreadyMatched,
                Message = "Trip request is already matched with a guide."
            };
        }

        tripRequest.Status = TripRequestStatus.Matched;
        tripRequest.MatchedGuideId = proposal.GuideId;
        tripRequest.AcceptedPrice = proposal.ProposedPrice;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        proposal.Status = ProposalStatus.Accepted;
        proposal.UpdatedAt = DateTime.UtcNow;

        var siblingProposals = await _context.Proposals
            .Where(p => p.TripRequestId == tripRequestId && p.Id != proposalId && p.Status == ProposalStatus.Submitted)
            .ToListAsync();

        foreach (var sibling in siblingProposals)
        {
            sibling.Status = ProposalStatus.Rejected;
            sibling.UpdatedAt = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;
        _context.UserNotifications.Add(new UserNotification
        {
            RecipientUserId = proposal.GuideId,
            Title = "Proposal accepted",
            Message = "Your proposal was accepted!",
            CreatedAt = now
        });

        foreach (var sibling in siblingProposals)
        {
            _context.UserNotifications.Add(new UserNotification
            {
                RecipientUserId = sibling.GuideId,
                Title = "Another proposal was selected",
                Message = "The Tourist selected another proposal for this request.",
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync();

        return new TripRequestLifecycleResultDto
        {
            Success = true,
            Message = "Proposal accepted. Trip request status updated to MATCHED.",
            Data = ToLifecycleDto(tripRequest)
        };
    }

    public async Task<TripRequestLifecycleResultDto> TransitionToPaymentPendingAsync(int touristId, int tripRequestId, decimal acceptedPrice)
    {
        var tripRequest = await _context.TripRequests
            .FirstOrDefaultAsync(t => t.Id == tripRequestId && t.TouristId == touristId);

        if (tripRequest == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        if (tripRequest.Status != TripRequestStatus.Matched)
        {
            var transitionError = TripStateMachine.GetTransitionError(tripRequest.Status, TripRequestStatus.PaymentPending);
            if (!string.IsNullOrEmpty(transitionError))
            {
                return new TripRequestLifecycleResultDto
                {
                    Success = false,
                    Error = TripLifecycleError.InvalidStatusTransition,
                    Message = transitionError
                };
            }
        }

        tripRequest.Status = TripRequestStatus.PaymentPending;
        tripRequest.AcceptedPrice = acceptedPrice;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TripRequestLifecycleResultDto
        {
            Success = true,
            Message = "Trip request status updated to PAYMENT_PENDING.",
            Data = ToLifecycleDto(tripRequest)
        };
    }

    public async Task<TripRequestLifecycleResultDto> TransitionToPlannedAsync(int touristId, int tripRequestId)
    {
        var tripRequest = await _context.TripRequests
            .FirstOrDefaultAsync(t => t.Id == tripRequestId && t.TouristId == touristId);

        if (tripRequest == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        var transitionError = TripStateMachine.GetTransitionError(tripRequest.Status, TripRequestStatus.Planned);
        if (!string.IsNullOrEmpty(transitionError))
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.InvalidStatusTransition,
                Message = transitionError
            };
        }

        tripRequest.Status = TripRequestStatus.Planned;
        tripRequest.PaymentCompletedAt = DateTime.UtcNow;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TripRequestLifecycleResultDto
        {
            Success = true,
            Message = "Trip request status updated to PLANNED.",
            Data = ToLifecycleDto(tripRequest)
        };
    }

    public async Task<TripRequestLifecycleResultDto> TransitionToInProgressAsync(int touristId, int tripRequestId)
    {
        var tripRequest = await _context.TripRequests
            .FirstOrDefaultAsync(t => t.Id == tripRequestId && t.TouristId == touristId);

        if (tripRequest == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        var transitionError = TripStateMachine.GetTransitionError(tripRequest.Status, TripRequestStatus.InProgress);
        if (!string.IsNullOrEmpty(transitionError))
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.InvalidStatusTransition,
                Message = transitionError
            };
        }

        if (tripRequest.Status == TripRequestStatus.InProgress)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripAlreadyStarted,
                Message = "Trip is already in progress."
            };
        }

        tripRequest.Status = TripRequestStatus.InProgress;
        tripRequest.StartedAt = DateTime.UtcNow;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TripRequestLifecycleResultDto
        {
            Success = true,
            Message = "Trip request status updated to IN_PROGRESS.",
            Data = ToLifecycleDto(tripRequest)
        };
    }

    public async Task<TripRequestLifecycleResultDto> TransitionToCompletedAsync(int touristId, int tripRequestId)
    {
        var tripRequest = await _context.TripRequests
            .FirstOrDefaultAsync(t => t.Id == tripRequestId && t.TouristId == touristId);

        if (tripRequest == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        var transitionError = TripStateMachine.GetTransitionError(tripRequest.Status, TripRequestStatus.Completed);
        if (!string.IsNullOrEmpty(transitionError))
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.InvalidStatusTransition,
                Message = transitionError
            };
        }

        if (tripRequest.Status == TripRequestStatus.Completed)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripAlreadyCompleted,
                Message = "Trip is already completed."
            };
        }

        tripRequest.Status = TripRequestStatus.Completed;
        tripRequest.CompletedAt = DateTime.UtcNow;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TripRequestLifecycleResultDto
        {
            Success = true,
            Message = "Trip request status updated to COMPLETED.",
            Data = ToLifecycleDto(tripRequest)
        };
    }

    public async Task<TripRequestLifecycleResultDto> TransitionToCancelledAsync(int touristId, int tripRequestId, string? reason)
    {
        var tripRequest = await _context.TripRequests
            .FirstOrDefaultAsync(t => t.Id == tripRequestId && t.TouristId == touristId);

        if (tripRequest == null)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        var transitionError = TripStateMachine.GetTransitionError(tripRequest.Status, TripRequestStatus.Cancelled);
        if (!string.IsNullOrEmpty(transitionError))
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.InvalidStatusTransition,
                Message = transitionError
            };
        }

        if (tripRequest.Status == TripRequestStatus.Cancelled)
        {
            return new TripRequestLifecycleResultDto
            {
                Success = false,
                Error = TripLifecycleError.TripAlreadyCancelled,
                Message = "Trip request is already cancelled."
            };
        }

        tripRequest.Status = TripRequestStatus.Cancelled;
        tripRequest.CancelledAt = DateTime.UtcNow;
        tripRequest.CancellationReason = reason;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TripRequestLifecycleResultDto
        {
            Success = true,
            Message = "Trip request cancelled successfully.",
            Data = ToLifecycleDto(tripRequest)
        };
    }

    private static TripRequestResponseDto ToResponse(TripRequest tripRequest)
    {
        return new TripRequestResponseDto
        {
            Id = tripRequest.Id,
            TouristId = tripRequest.TouristId,
            Destination = tripRequest.Destination,
            TripType = tripRequest.TripType,
            StartDate = tripRequest.StartDate,
            EndDate = tripRequest.EndDate,
            NumberOfTravelers = tripRequest.NumberOfTravelers,
            Budget = tripRequest.Budget,
            BudgetMin = tripRequest.BudgetMin,
            BudgetMax = tripRequest.BudgetMax,
            Description = tripRequest.Description,
            TravelPreferences = tripRequest.TravelPreferences,
            Status = tripRequest.Status.ToString(),
            CreatedAt = tripRequest.CreatedAt,
            UpdatedAt = tripRequest.UpdatedAt
        };
    }

    private static TripRequestLifecycleDto ToLifecycleDto(TripRequest tripRequest)
    {
        var allowedTransitions = TripStateMachine.GetAllowedTransitions(tripRequest.Status)
            .Select(s => s.ToString())
            .ToList();

        return new TripRequestLifecycleDto
        {
            Id = tripRequest.Id,
            TouristId = tripRequest.TouristId,
            Destination = tripRequest.Destination,
            TripType = tripRequest.TripType,
            StartDate = tripRequest.StartDate,
            EndDate = tripRequest.EndDate,
            NumberOfTravelers = tripRequest.NumberOfTravelers,
            Budget = tripRequest.Budget,
            BudgetMin = tripRequest.BudgetMin,
            BudgetMax = tripRequest.BudgetMax,
            Description = tripRequest.Description,
            TravelPreferences = tripRequest.TravelPreferences,
            Status = tripRequest.Status.ToString(),
            MatchedGuideId = tripRequest.MatchedGuideId,
            AcceptedPrice = tripRequest.AcceptedPrice,
            CreatedAt = tripRequest.CreatedAt,
            UpdatedAt = tripRequest.UpdatedAt,
            PaymentCompletedAt = tripRequest.PaymentCompletedAt,
            StartedAt = tripRequest.StartedAt,
            CompletedAt = tripRequest.CompletedAt,
            CancelledAt = tripRequest.CancelledAt,
            CancellationReason = tripRequest.CancellationReason,
            AllowedTransitions = allowedTransitions
        };
    }
}