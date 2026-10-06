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

    public ProposalService(
        ApplicationDbContext context,
        ILogger<ProposalService> logger)
    {
        _context = context;
        _logger = logger;
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
                .AsNoTracking()
                .Include(p => p.Guide)
                .Where(p => p.TripRequestId == tripRequestId)
                .Where(p => p.Status != ProposalStatus.Draft)
                .ToListAsync();

            var guideUserIds = proposals
                .Select(p => p.GuideId)
                .Distinct()
                .ToArray();

            var guideProfiles = await _context.GuideProfiles
                .AsNoTracking()
                .Where(gp => guideUserIds.Contains(gp.UserId))
                .ToDictionaryAsync(gp => gp.UserId, gp => gp);

            proposals = ApplySorting(proposals, sort, guideProfiles);

            if (proposals.Count == 0)
            {
                return new ProposalListResultDto
                {
                    Success = true,
                    Error = ProposalError.NoProposals,
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
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load proposals for trip request {TripRequestId} and tourist {TouristUserId}.",
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
                .AsNoTracking()
                .Include(p => p.Guide)
                .Include(p => p.ItineraryDays)
                .Where(p =>
                    p.Id == proposalId &&
                    p.TripRequestId == tripRequestId &&
                    p.Status != ProposalStatus.Draft)
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
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load proposal detail {ProposalId} for trip request {TripRequestId}.",
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
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

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

            if (tripRequest.Status is not (TripRequestStatus.Open or TripRequestStatus.Pending))
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
            var matchedRows = await _context.TripRequests
                .Where(t =>
                    t.Id == tripRequestId &&
                    t.TouristId == touristUserId &&
                    (t.Status == TripRequestStatus.Open ||
                     t.Status == TripRequestStatus.Pending))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, TripRequestStatus.Matched)
                    .SetProperty(t => t.MatchedGuideId, proposal.GuideId)
                    .SetProperty(t => t.UpdatedAt, now));

            if (matchedRows == 0)
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
            await transaction.CommitAsync();

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
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to accept proposal {ProposalId} for trip request {TripRequestId}.",
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

    private static List<Proposal> ApplySorting(
        List<Proposal> proposals,
        ProposalSort sort,
        IReadOnlyDictionary<int, GuideProfile> guideProfiles)
    {
        var query = proposals.AsEnumerable();

        query = sort switch
        {
            ProposalSort.PriceLowToHigh =>
                query.OrderBy(p => p.Price)
                    .ThenByDescending(p => p.CreatedAt),

            ProposalSort.PriceHighToLow =>
                query.OrderByDescending(p => p.Price)
                    .ThenByDescending(p => p.CreatedAt),

            ProposalSort.RatingHighToLow =>
                query.OrderByDescending(p =>
                        guideProfiles.TryGetValue(p.GuideId, out var profile)
                            ? profile.AverageRating
                            : null)
                    .ThenByDescending(p => p.CreatedAt),

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
            Price = proposal.Price,
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
            tripRequest.Status is TripRequestStatus.Open or TripRequestStatus.Pending &&
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
            Price = proposal.Price,
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
