
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.DTOs.Proposals;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;
using Npgsql;

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
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfTravelers = request.NumberOfTravelers,
            Budget = request.Budget,
            Description = request.Description?.Trim() ?? string.Empty,
            Status = TripRequestStatus.Open,
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

    public async Task<ProposalResultDto> CreateProposalAsync(
        int guideId,
        int tripRequestId,
        CreateProposalDto request)
    {
        var guide = await (
            from profile in _context.GuideProfiles.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on profile.UserId equals user.Id
            where profile.UserId == guideId
            select new
            {
                profile.VerificationStatus,
                user.IsActive,
                user.Role
            })
            .SingleOrDefaultAsync();

        if (guide is null ||
            !guide.IsActive ||
            guide.Role != UserRole.Guide ||
            guide.VerificationStatus != VerificationStatus.Verified)
        {
            return new ProposalResultDto
            {
                Error = ProposalError.GuideNotVerified,
                Message = "You must be verified to submit proposals."
            };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var tripRequest = await _context.TripRequests
            .FromSqlInterpolated($"SELECT * FROM \"TripRequests\" WHERE \"Id\" = {tripRequestId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync();

        if (tripRequest is null)
        {
            return new ProposalResultDto
            {
                Error = ProposalError.TripRequestNotFound,
                Message = "Trip request was not found."
            };
        }

        if (tripRequest.Status != TripRequestStatus.Open)
        {
            return new ProposalResultDto
            {
                Error = ProposalError.TripRequestNotOpen,
                Message = "This trip request is no longer accepting proposals."
            };
        }

        var alreadySubmitted = await _context.Proposals.AnyAsync(proposal =>
            proposal.GuideId == guideId &&
            proposal.TripRequestId == tripRequestId &&
            proposal.Status == ProposalStatus.Pending);

        if (alreadySubmitted)
        {
            return new ProposalResultDto
            {
                Error = ProposalError.DuplicateProposal,
                Message = "You've already submitted a proposal for this request."
            };
        }

        var proposal = new Proposal
        {
            GuideId = guideId,
            TripRequestId = tripRequestId,
            Price = request.Price,
            EstimatedExpenses = request.EstimatedExpenses,
            Availability = request.Availability.Trim(),
            Inclusions = request.Inclusions.Trim(),
            Exclusions = string.IsNullOrWhiteSpace(request.Exclusions)
                ? null
                : request.Exclusions.Trim(),
            AdditionalNotes = string.IsNullOrWhiteSpace(request.AdditionalNotes)
                ? null
                : request.AdditionalNotes.Trim(),
            Status = ProposalStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };

        _context.Proposals.Add(proposal);
        _context.Notifications.Add(new Notification
        {
            RecipientUserId = tripRequest.TouristId,
            TripRequestId = tripRequest.Id,
            Title = "New proposal received",
            Message = $"You received a new proposal for {tripRequest.Destination} trip.",
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Proposals_GuideId_TripRequestId"
            })
        {
            return new ProposalResultDto
            {
                Error = ProposalError.DuplicateProposal,
                Message = "You've already submitted a proposal for this request."
            };
        }

        return new ProposalResultDto
        {
            Success = true,
            Message = "Proposal submitted!",
            Data = ToResponse(proposal, tripRequest)
        };
    }

    public async Task<IReadOnlyList<ProposalResponseDto>> GetProposalsByGuideAsync(
        int guideId)
    {
        return await _context.Proposals
            .AsNoTracking()
            .Where(proposal => proposal.GuideId == guideId)
            .Join(
                _context.TripRequests.AsNoTracking(),
                proposal => proposal.TripRequestId,
                tripRequest => tripRequest.Id,
                (proposal, tripRequest) => ToResponse(proposal, tripRequest))
            .OrderByDescending(proposal => proposal.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TripRequestResponseDto>> GetOpenTripRequestsAsync()
    {
        var tripRequests = await _context.TripRequests
            .AsNoTracking()
            .Where(tripRequest => tripRequest.Status == TripRequestStatus.Open)
            .OrderBy(tripRequest => tripRequest.StartDate)
            .ToListAsync();

        return tripRequests.Select(ToResponse).ToList();
    }

    public async Task<TripRequestResponseDto?> GetOpenTripRequestAsync(int tripRequestId)
    {
        var tripRequest = await _context.TripRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(request =>
                request.Id == tripRequestId &&
                request.Status == TripRequestStatus.Open);

        return tripRequest is null ? null : ToResponse(tripRequest);
    }

    private static TripRequestResponseDto ToResponse(
        TripRequest tripRequest)
    {
        return new TripRequestResponseDto
        {
            Id = tripRequest.Id,
            TouristId = tripRequest.TouristId,
            Destination = tripRequest.Destination,
            StartDate = tripRequest.StartDate,
            EndDate = tripRequest.EndDate,
            NumberOfTravelers = tripRequest.NumberOfTravelers,
            Budget = tripRequest.Budget,
            Description = tripRequest.Description,
            Status = tripRequest.Status.ToString(),
            CreatedAt = tripRequest.CreatedAt,
            UpdatedAt = tripRequest.UpdatedAt
        };
    }

    private static ProposalResponseDto ToResponse(
        Proposal proposal,
        TripRequest tripRequest)
    {
        return new ProposalResponseDto
        {
            Id = proposal.Id,
            GuideId = proposal.GuideId,
            TripRequestId = proposal.TripRequestId,
            Destination = tripRequest.Destination,
            StartDate = tripRequest.StartDate,
            EndDate = tripRequest.EndDate,
            TripRequestBudget = tripRequest.Budget,
            Price = proposal.Price,
            EstimatedExpenses = proposal.EstimatedExpenses,
            Availability = proposal.Availability,
            Inclusions = proposal.Inclusions,
            Exclusions = proposal.Exclusions,
            AdditionalNotes = proposal.AdditionalNotes,
            Status = proposal.Status.ToString(),
            SubmittedAt = proposal.SubmittedAt
        };
    }
}

