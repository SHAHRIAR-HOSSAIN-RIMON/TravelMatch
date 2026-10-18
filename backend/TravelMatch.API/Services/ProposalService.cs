using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Proposals;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class ProposalService : IProposalService
{
    private const int SummaryLength = 120;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProposalService> _logger;
    private readonly INotificationService _notificationService;

    public ProposalService(
        ApplicationDbContext context,
        ILogger<ProposalService> logger,
        INotificationService notificationService)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<ProposalListResultDto> GetProposalsAsync(
        int touristUserId,
        int tripRequestId,
        ProposalSort sort)
    {
        if (tripRequestId <= 0)
        {
            return new ProposalListResultDto
            {
                Success = false,
                Error = ProposalError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        try
        {
            var tripRequest = await _context.TripRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tripRequestId);

            if (tripRequest is null)
            {
                return new ProposalListResultDto
                {
                    Success = false,
                    Error = ProposalError.TripRequestNotFound,
                    Message = "Trip request not found."
                };
            }

            if (tripRequest.TouristId != touristUserId)
            {
                return new ProposalListResultDto
                {
                    Success = false,
                    Error = ProposalError.Unauthorized,
                    Message = "You are not authorized to view this."
                };
            }

            var proposals = await _context.Proposals
                .Include(p => p.Guide)
                .Where(p => p.TripRequestId == tripRequestId)
                .Where(p => p.Status == ProposalStatus.Submitted)
                .ToListAsync();

            var guideUserIds = proposals
                .Select(p => p.GuideId)
                .Distinct()
                .ToArray();

            var guideProfiles = await _context.GuideProfiles
                .AsNoTracking()
                .Where(gp => guideUserIds.Contains(gp.UserId))
                .ToDictionaryAsync(gp => gp.UserId, gp => gp);

            foreach (var proposal in proposals)
            {
                var matchResult = ProposalMatchCalculator.Calculate(
                    tripRequest,
                    proposal,
                    guideProfiles.GetValueOrDefault(proposal.GuideId));

                proposal.MatchingScore = matchResult.TotalScore;
            }

            proposals = ApplySorting(proposals, sort, guideProfiles);

            if (proposals.Count == 0)
            {
                return new ProposalListResultDto
                {
                    Success = true,
                    Error = ProposalError.None,
                    Message = "No proposals yet. Guides will be notified of your request and can submit offers soon.",
                    Data = Array.Empty<ProposalDto>(),
                    TotalCount = 0,
                    AppliedSort = sort
                };
            }

            var dtos = proposals
                .Select(p => MapToDto(p, guideProfiles.GetValueOrDefault(p.GuideId)))
                .ToList();

            return new ProposalListResultDto
            {
                Success = true,
                Error = ProposalError.None,
                Message = $"Showing {dtos.Count} proposal(s).",
                Data = dtos,
                TotalCount = dtos.Count,
                AppliedSort = sort
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Request cancelled while loading proposals for trip request {TripRequestId}.",
                tripRequestId);

            return new ProposalListResultDto
            {
                Success = false,
                Error = ProposalError.ServerError,
                Message = "The request was cancelled. Please try again."
            };
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Database error loading proposals for trip request {TripRequestId} and tourist {TouristUserId}.",
                tripRequestId,
                touristUserId);

            return new ProposalListResultDto
            {
                Success = false,
                Error = ProposalError.ServerError,
                Message = "Couldn't load proposals. Retry?"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error loading proposals for trip request {TripRequestId} and tourist {TouristUserId}.",
                tripRequestId,
                touristUserId);

            return new ProposalListResultDto
            {
                Success = false,
                Error = ProposalError.ServerError,
                Message = "Couldn't load proposals. Retry?"
            };
        }
    }

    public async Task<ProposalDetailResultDto> GetProposalDetailAsync(
        int touristUserId,
        int tripRequestId,
        int proposalId)
    {
        if (tripRequestId <= 0 || proposalId <= 0)
        {
            return new ProposalDetailResultDto
            {
                Success = false,
                Error = ProposalError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        try
        {
            var tripRequest = await _context.TripRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tripRequestId);

            if (tripRequest is null)
            {
                return new ProposalDetailResultDto
                {
                    Success = false,
                    Error = ProposalError.TripRequestNotFound,
                    Message = "Trip request not found."
                };
            }

            if (tripRequest.TouristId != touristUserId)
            {
                return new ProposalDetailResultDto
                {
                    Success = false,
                    Error = ProposalError.Unauthorized,
                    Message = "You are not authorized to view this."
                };
            }

            var proposal = await _context.Proposals
                .Include(p => p.Guide)
                .Include(p => p.ItineraryDays)
                .Where(p =>
                    p.Id == proposalId &&
                    p.TripRequestId == tripRequestId &&
                    p.Status == ProposalStatus.Submitted)
                .FirstOrDefaultAsync();

            if (proposal is null)
            {
                return new ProposalDetailResultDto
                {
                    Success = false,
                    Error = ProposalError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            var guideProfile = await _context.GuideProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.UserId == proposal.GuideId);

            var scoreResult = ProposalMatchCalculator.Calculate(
                tripRequest,
                proposal,
                guideProfile);

            proposal.MatchingScore = scoreResult.TotalScore;

            var isVerificationWarning =
                guideProfile?.VerificationStatus != VerificationStatus.Verified;

            var dto = MapToDetailDto(
                proposal,
                guideProfile,
                isVerificationWarning,
                tripRequest);

            return new ProposalDetailResultDto
            {
                Success = true,
                Error = ProposalError.None,
                Message = "Proposal loaded successfully.",
                Data = dto
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Request cancelled while loading proposal detail {ProposalId} for trip request {TripRequestId}.",
                proposalId,
                tripRequestId);

            return new ProposalDetailResultDto
            {
                Success = false,
                Error = ProposalError.ServerError,
                Message = "The request was cancelled. Please try again."
            };
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Database error loading proposal detail {ProposalId} for trip request {TripRequestId}.",
                proposalId,
                tripRequestId);

            return new ProposalDetailResultDto
            {
                Success = false,
                Error = ProposalError.ServerError,
                Message = "Couldn't load proposal. Please try again."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error loading proposal detail {ProposalId} for trip request {TripRequestId}.",
                proposalId,
                tripRequestId);

            return new ProposalDetailResultDto
            {
                Success = false,
                Error = ProposalError.ServerError,
                Message = "Couldn't load proposal. Please try again."
            };
        }
    }

    public async Task<AcceptProposalResultDto> AcceptProposalAsync(
        int touristUserId,
        int tripRequestId,
        int proposalId)
    {
        if (tripRequestId <= 0 || proposalId <= 0)
        {
            return new AcceptProposalResultDto
            {
                Success = false,
                Error = AcceptProposalError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        try
        {
            var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

            await using var _ = transaction;

            var tripRequest = await _context.TripRequests
                .FirstOrDefaultAsync(t => t.Id == tripRequestId);

            if (tripRequest is null)
            {
                return new AcceptProposalResultDto
                {
                    Success = false,
                    Error = AcceptProposalError.TripRequestNotFound,
                    Message = "Trip request not found."
                };
            }

            if (tripRequest.TouristId != touristUserId)
            {
                return new AcceptProposalResultDto
                {
                    Success = false,
                    Error = AcceptProposalError.Unauthorized,
                    Message = "You are not authorized to perform this action."
                };
            }

            if (tripRequest.Status == TripRequestStatus.Matched)
            {
                return new AcceptProposalResultDto
                {
                    Success = false,
                    Error = AcceptProposalError.TripRequestAlreadyMatched,
                    Message = "You've already accepted a proposal for this trip request."
                };
            }

            if (tripRequest.Status is not (TripRequestStatus.Pending or TripRequestStatus.ProposalsReceived))
            {
                return new AcceptProposalResultDto
                {
                    Success = false,
                    Error = AcceptProposalError.TripRequestNotAvailable,
                    Message = "This trip request is no longer available."
                };
            }

            var proposal = await _context.Proposals
                .FirstOrDefaultAsync(p =>
                    p.Id == proposalId &&
                    p.TripRequestId == tripRequestId);

            if (proposal is null)
            {
                return new AcceptProposalResultDto
                {
                    Success = false,
                    Error = AcceptProposalError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.Status != ProposalStatus.Submitted)
            {
                return new AcceptProposalResultDto
                {
                    Success = false,
                    Error = AcceptProposalError.ProposalNotAvailable,
                    Message = "This proposal is no longer available. Please choose another."
                };
            }

            var siblingProposals = await _context.Proposals
                .Where(p =>
                    p.TripRequestId == tripRequestId &&
                    p.Id != proposalId &&
                    p.Status == ProposalStatus.Submitted)
                .ToListAsync();

            var now = DateTime.UtcNow;

            var tripRequestToMatch = await _context.TripRequests
                .FirstOrDefaultAsync(t =>
                    t.Id == tripRequestId &&
                    t.TouristId == touristUserId &&
                    (t.Status == TripRequestStatus.Pending ||
                     t.Status == TripRequestStatus.ProposalsReceived));

            if (tripRequestToMatch is null)
            {
                var currentStatus = await _context.TripRequests
                    .AsNoTracking()
                    .Where(t => t.Id == tripRequestId)
                    .Select(t => (TripRequestStatus?)t.Status)
                    .FirstOrDefaultAsync();

                var alreadyMatched = currentStatus == TripRequestStatus.Matched;
                return new AcceptProposalResultDto
                {
                    Success = false,
                    Error = alreadyMatched
                        ? AcceptProposalError.TripRequestAlreadyMatched
                        : AcceptProposalError.TripRequestNotAvailable,
                    Message = alreadyMatched
                        ? "You've already accepted a proposal for this trip request."
                        : "This trip request is no longer available."
                };
            }

            tripRequestToMatch.Status = TripRequestStatus.Matched;
            tripRequestToMatch.MatchedGuideId = proposal.GuideId;
            tripRequestToMatch.UpdatedAt = now;

            proposal.Status = ProposalStatus.Accepted;
            proposal.UpdatedAt = now;

            foreach (var sibling in siblingProposals)
            {
                sibling.Status = ProposalStatus.Rejected;
                sibling.UpdatedAt = now;
            }

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

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }

            return new AcceptProposalResultDto
            {
                Success = true,
                Error = AcceptProposalError.None,
                Message = "Proposal accepted successfully. You will be redirected to payment.",
                AcceptedProposalId = proposal.Id,
                TripRequestId = tripRequest.Id,
                GuideId = proposal.GuideId
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Request cancelled while accepting proposal {ProposalId} for trip request {TripRequestId}.",
                proposalId,
                tripRequestId);

            return new AcceptProposalResultDto
            {
                Success = false,
                Error = AcceptProposalError.ServerError,
                Message = "The request was cancelled. Please try again."
            };
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Database error accepting proposal {ProposalId} for trip request {TripRequestId}.",
                proposalId,
                tripRequestId);

            return new AcceptProposalResultDto
            {
                Success = false,
                Error = AcceptProposalError.ServerError,
                Message = "Failed to accept proposal. Please try again."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error accepting proposal {ProposalId} for trip request {TripRequestId}.",
                proposalId,
                tripRequestId);

            return new AcceptProposalResultDto
            {
                Success = false,
                Error = AcceptProposalError.ServerError,
                Message = "Failed to accept proposal. Please try again."
            };
        }
    }

    public async Task<SubmitProposalResultDto> SubmitProposalAsync(
        int guideUserId,
        int tripRequestId,
        int proposalId,
        SubmitProposalDto request)
    {
        if (tripRequestId <= 0 || proposalId <= 0)
        {
            return new SubmitProposalResultDto
            {
                Success = false,
                Error = SubmitProposalError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        try
        {
            // Verify guide is active
            var guideExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == guideUserId && u.Role == UserRole.Guide && u.IsActive);

            if (!guideExists)
            {
                return new SubmitProposalResultDto
                {
                    Success = false,
                    Error = SubmitProposalError.GuideNotFound,
                    Message = "Guide account not found or inactive."
                };
            }

            // Require guide to be verified before submitting
            var verificationStatus = await _context.GuideProfiles
                .AsNoTracking()
                .Where(gp => gp.UserId == guideUserId)
                .Select(gp => (VerificationStatus?)gp.VerificationStatus)
                .FirstOrDefaultAsync();

            if (verificationStatus != VerificationStatus.Verified)
            {
                return new SubmitProposalResultDto
                {
                    Success = false,
                    Error = SubmitProposalError.GuideNotVerified,
                    Message = "Only verified guides can submit proposals."
                };
            }

            var proposal = await _context.Proposals
                .Include(p => p.ItineraryDays)
                .FirstOrDefaultAsync(p => p.Id == proposalId && p.TripRequestId == tripRequestId);

            if (proposal is null)
            {
                return new SubmitProposalResultDto
                {
                    Success = false,
                    Error = SubmitProposalError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new SubmitProposalResultDto
                {
                    Success = false,
                    Error = SubmitProposalError.Unauthorized,
                    Message = "You are not authorized to submit this proposal."
                };
            }

            if (proposal.Status != ProposalStatus.Draft)
            {
                return new SubmitProposalResultDto
                {
                    Success = false,
                    Error = SubmitProposalError.ProposalNotInDraftState,
                    Message = "Only proposals in Draft status can be submitted."
                };
            }

            // Ensure the trip request is still open
            var tripRequest = await _context.TripRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tripRequestId);

            if (tripRequest is null)
            {
                return new SubmitProposalResultDto
                {
                    Success = false,
                    Error = SubmitProposalError.TripRequestNotFound,
                    Message = "Trip request not found."
                };
            }

            if (tripRequest.Status != TripRequestStatus.Pending &&
                tripRequest.Status != TripRequestStatus.ProposalsReceived)
            {
                return new SubmitProposalResultDto
                {
                    Success = false,
                    Error = SubmitProposalError.TripRequestNotAcceptingProposals,
                    Message = "This trip request is no longer accepting proposals."
                };
            }

            // Apply submission fields and transition to Submitted
            proposal.Availability = request.Availability.Trim();
            proposal.Inclusions = request.Inclusions.Trim();
            proposal.Exclusions = request.Exclusions.Trim();
            proposal.Notes = request.Notes?.Trim() ?? string.Empty;
            proposal.Status = ProposalStatus.Submitted;
            proposal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify the tourist that a new proposal was submitted
            var touristProfile = await _context.TouristProfiles
                .AsNoTracking()
                .Where(tp => tp.Id == tripRequest.TouristId)
                .Select(tp => tp.UserId)
                .FirstOrDefaultAsync();

            if (touristProfile > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    touristProfile,
                    "New proposal received",
                    $"A guide submitted a proposal for your trip to {tripRequest.Destination}.");
            }

            return new SubmitProposalResultDto
            {
                Success = true,
                Message = "Proposal submitted successfully. The tourist will be notified.",
                Data = MapToGuideProposalDto(proposal, tripRequest)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to submit proposal {ProposalId} for trip request {TripRequestId} by guide {GuideUserId}.",
                proposalId, tripRequestId, guideUserId);

            return new SubmitProposalResultDto
            {
                Success = false,
                Error = SubmitProposalError.ServerError,
                Message = "Failed to submit proposal. Please try again."
            };
        }
    }

    public async Task<UpdateProposalResultDto> UpdateProposalAsync(
        int guideUserId,
        int tripRequestId,
        int proposalId,
        UpdateProposalDto request)
    {
        if (tripRequestId <= 0 || proposalId <= 0)
        {
            return new UpdateProposalResultDto
            {
                Success = false,
                Error = UpdateProposalError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        try
        {
            var proposal = await _context.Proposals
                .Include(p => p.ItineraryDays)
                .FirstOrDefaultAsync(p => p.Id == proposalId && p.TripRequestId == tripRequestId);

            if (proposal is null)
            {
                return new UpdateProposalResultDto
                {
                    Success = false,
                    Error = UpdateProposalError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new UpdateProposalResultDto
                {
                    Success = false,
                    Error = UpdateProposalError.Unauthorized,
                    Message = "You are not authorized to update this proposal."
                };
            }

            // Only Draft proposals can be freely edited; Submitted can also be updated
            // (guide may want to revise before it is accepted)
            if (proposal.Status == ProposalStatus.Accepted ||
                proposal.Status == ProposalStatus.Rejected ||
                proposal.Status == ProposalStatus.Withdrawn)
            {
                return new UpdateProposalResultDto
                {
                    Success = false,
                    Error = UpdateProposalError.ProposalNotEditable,
                    Message = $"Proposals in '{proposal.Status}' status cannot be edited."
                };
            }

            if (request.ProposedPrice.HasValue && request.ProposedPrice.Value <= 0)
            {
                return new UpdateProposalResultDto
                {
                    Success = false,
                    Error = UpdateProposalError.ValidationError,
                    Message = "Proposed price must be greater than zero."
                };
            }

            // Apply partial updates
            if (request.ProposedPrice.HasValue)
                proposal.ProposedPrice = request.ProposedPrice.Value;
            if (request.Message is not null)
                proposal.Message = request.Message.Trim();
            if (request.Availability is not null)
                proposal.Availability = request.Availability.Trim();
            if (request.Inclusions is not null)
                proposal.Inclusions = request.Inclusions.Trim();
            if (request.Exclusions is not null)
                proposal.Exclusions = request.Exclusions.Trim();
            if (request.Notes is not null)
                proposal.Notes = request.Notes.Trim();

            proposal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var tripRequest = await _context.TripRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tripRequestId);

            return new UpdateProposalResultDto
            {
                Success = true,
                Message = "Proposal updated successfully.",
                Data = MapToGuideProposalDto(proposal, tripRequest)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update proposal {ProposalId} for trip request {TripRequestId} by guide {GuideUserId}.",
                proposalId, tripRequestId, guideUserId);

            return new UpdateProposalResultDto
            {
                Success = false,
                Error = UpdateProposalError.ServerError,
                Message = "Failed to update proposal. Please try again."
            };
        }
    }

    public async Task<WithdrawProposalResultDto> WithdrawProposalAsync(
        int guideUserId,
        int tripRequestId,
        int proposalId)
    {
        if (tripRequestId <= 0 || proposalId <= 0)
        {
            return new WithdrawProposalResultDto
            {
                Success = false,
                Error = WithdrawProposalError.ProposalNotFound,
                Message = "Proposal not found."
            };
        }

        try
        {
            var proposal = await _context.Proposals
                .FirstOrDefaultAsync(p => p.Id == proposalId && p.TripRequestId == tripRequestId);

            if (proposal is null)
            {
                return new WithdrawProposalResultDto
                {
                    Success = false,
                    Error = WithdrawProposalError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.GuideId != guideUserId)
            {
                return new WithdrawProposalResultDto
                {
                    Success = false,
                    Error = WithdrawProposalError.Unauthorized,
                    Message = "You are not authorized to withdraw this proposal."
                };
            }

            if (proposal.Status == ProposalStatus.Withdrawn)
            {
                return new WithdrawProposalResultDto
                {
                    Success = false,
                    Error = WithdrawProposalError.ProposalAlreadyWithdrawn,
                    Message = "Proposal has already been withdrawn."
                };
            }

            if (proposal.Status == ProposalStatus.Accepted)
            {
                return new WithdrawProposalResultDto
                {
                    Success = false,
                    Error = WithdrawProposalError.ProposalAlreadyAccepted,
                    Message = "An accepted proposal cannot be withdrawn."
                };
            }

            if (proposal.Status == ProposalStatus.Rejected)
            {
                return new WithdrawProposalResultDto
                {
                    Success = false,
                    Error = WithdrawProposalError.ProposalAlreadyRejected,
                    Message = "A rejected proposal cannot be withdrawn."
                };
            }

            proposal.Status = ProposalStatus.Withdrawn;
            proposal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify the tourist that the proposal was withdrawn
            var tripRequest = await _context.TripRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tripRequestId);

            if (tripRequest is not null)
            {
                var touristProfile = await _context.TouristProfiles
                    .AsNoTracking()
                    .Where(tp => tp.Id == tripRequest.TouristId)
                    .Select(tp => tp.UserId)
                    .FirstOrDefaultAsync();

                if (touristProfile > 0)
                {
                    await _notificationService.CreateNotificationAsync(
                        touristProfile,
                        "Proposal withdrawn",
                        $"A guide withdrew their proposal for your trip to {tripRequest.Destination}.");
                }
            }

            return new WithdrawProposalResultDto
            {
                Success = true,
                Message = "Proposal withdrawn successfully."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to withdraw proposal {ProposalId} for trip request {TripRequestId} by guide {GuideUserId}.",
                proposalId, tripRequestId, guideUserId);

            return new WithdrawProposalResultDto
            {
                Success = false,
                Error = WithdrawProposalError.ServerError,
                Message = "Failed to withdraw proposal. Please try again."
            };
        }
    }

    public async Task<GuideProposalListResultDto> GetMyProposalsAsync(int guideUserId)
    {
        try
        {
            var proposals = await _context.Proposals
                .AsNoTracking()
                .Include(p => p.TripRequest)
                .Include(p => p.ItineraryDays)
                .Where(p => p.GuideId == guideUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            var dtos = proposals
                .Select(p => MapToGuideProposalDto(p, p.TripRequest))
                .ToList();

            return new GuideProposalListResultDto
            {
                Success = true,
                Message = dtos.Count == 0
                    ? "You have not submitted any proposals yet."
                    : $"Showing {dtos.Count} proposal(s).",
                Data = dtos,
                TotalCount = dtos.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load proposals for guide {GuideUserId}.", guideUserId);

            return new GuideProposalListResultDto
            {
                Success = false,
                Error = ProposalError.ServerError,
                Message = "Couldn't load proposals. Please try again."
            };
        }
    }

    private static GuideProposalDto MapToGuideProposalDto(Proposal proposal, TripRequest? tripRequest)
    {
        return new GuideProposalDto
        {
            Id = proposal.Id,
            TripRequestId = proposal.TripRequestId,
            TripRequestDestination = tripRequest?.Destination ?? string.Empty,
            TripRequestStartDate = tripRequest?.StartDate ?? default,
            TripRequestEndDate = tripRequest?.EndDate ?? default,
            ProposedPrice = proposal.ProposedPrice,
            Message = proposal.Message,
            Availability = proposal.Availability,
            Inclusions = proposal.Inclusions,
            Exclusions = proposal.Exclusions,
            Notes = proposal.Notes,
            Status = proposal.Status.ToString(),
            ItineraryDayCount = proposal.ItineraryDays?.Count ?? 0,
            CreatedAt = proposal.CreatedAt,
            UpdatedAt = proposal.UpdatedAt
        };
    }

    private static List<Proposal> ApplySorting(
        List<Proposal> proposals,
        ProposalSort sort,
        IReadOnlyDictionary<int, GuideProfile> guideProfiles)
    {
        var query = proposals.AsEnumerable();

        query = sort switch
        {
            ProposalSort.PriceLowToHigh =>
                query.OrderBy(p => p.ProposedPrice)
                    .ThenByDescending(p => p.CreatedAt),

            ProposalSort.PriceHighToLow =>
                query.OrderByDescending(p => p.ProposedPrice)
                    .ThenByDescending(p => p.CreatedAt),

            ProposalSort.RatingHighToLow =>
                query.OrderByDescending(p =>
                        guideProfiles.TryGetValue(p.GuideId, out var profile)
                            ? profile.AverageRating
                            : null)
                    .ThenByDescending(p => p.CreatedAt),

            ProposalSort.BestMatch =>
                query.OrderByDescending(p => p.MatchingScore ?? 0m)
                    .ThenBy(p => p.Id),

            _ =>
                query.OrderByDescending(p => p.CreatedAt)
                    .ThenByDescending(p => p.Id)
        };

        return query.ToList();
    }

    private static ProposalDto MapToDto(
        Proposal proposal,
        GuideProfile? guideProfile)
    {
        return new ProposalDto
        {
            Id = proposal.Id,
            Guide = new GuideInfoDto
            {
                Id = proposal.GuideId,
                Name = proposal.Guide.FullName,
                PhotoUrl = guideProfile?.PhotoUrl,
                AverageRating = guideProfile?.AverageRating,
                ExperienceYears = guideProfile?.ExperienceYears ?? 0,
                ServiceArea = guideProfile?.ServiceArea ?? string.Empty,
                VerificationStatus = guideProfile?.VerificationStatus.ToString() ?? string.Empty,
                IsVerified = guideProfile?.VerificationStatus == VerificationStatus.Verified
            },
            Price = proposal.ProposedPrice,
            Availability = proposal.Availability,
            InclusionsSummary = Truncate(proposal.Inclusions),
            ExclusionsSummary = Truncate(proposal.Exclusions),
            MatchingScore = proposal.MatchingScore,
            Status = proposal.Status.ToString(),
            CreatedAt = proposal.CreatedAt
        };
    }

    private static ProposalDetailDto MapToDetailDto(
        Proposal proposal,
        GuideProfile? guideProfile,
        bool isVerificationWarning,
        TripRequest tripRequest)
    {
        var itinerary = proposal.ItineraryDays?
            .OrderBy(i => i.DayNumber)
            .Select(i => new ItineraryDayDto
            {
                DayNumber = i.DayNumber,
                Title = i.Title,
                Description = i.Description,
                Activities = i.Activities,
                Accommodation = i.Accommodation,
                Meals = i.Meals
            })
            .ToArray() ?? Array.Empty<ItineraryDayDto>();

        var isEligibleForAcceptance =
            tripRequest.Status is TripRequestStatus.Pending or TripRequestStatus.ProposalsReceived &&
            proposal.Status == ProposalStatus.Submitted;

        return new ProposalDetailDto
        {
            Id = proposal.Id,
            Guide = new GuideInfoDto
            {
                Id = proposal.GuideId,
                Name = proposal.Guide.FullName,
                PhotoUrl = guideProfile?.PhotoUrl,
                AverageRating = guideProfile?.AverageRating,
                ExperienceYears = guideProfile?.ExperienceYears ?? 0,
                ServiceArea = guideProfile?.ServiceArea ?? string.Empty,
                VerificationStatus = guideProfile?.VerificationStatus.ToString() ?? string.Empty,
                IsVerified = guideProfile?.VerificationStatus == VerificationStatus.Verified
            },
            Price = proposal.ProposedPrice,
            Availability = proposal.Availability,
            Inclusions = proposal.Inclusions,
            Exclusions = proposal.Exclusions,
            Notes = proposal.Notes,
            Itinerary = itinerary,
            MatchingScore = proposal.MatchingScore,
            Status = proposal.Status.ToString(),
            IsVerificationWarning = isVerificationWarning,
            IsEligibleForAcceptance = isEligibleForAcceptance,
            CreatedAt = proposal.CreatedAt,
            UpdatedAt = proposal.UpdatedAt
        };
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length <= SummaryLength)
        {
            return value;
        }

        return value.Substring(0, SummaryLength) + "...";
    }
}
