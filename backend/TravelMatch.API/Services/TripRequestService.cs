
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

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
            TripType = string.IsNullOrWhiteSpace(request.TripType)
                ? "Other"
                : request.TripType.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfTravelers = request.NumberOfTravelers,
            Budget = request.Budget,
            Description = request.Description?.Trim() ?? string.Empty,
            TravelPreferences = request.TravelPreferences?.Trim() ?? string.Empty,
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

    public async Task<IReadOnlyList<AvailableTripRequestDto>> GetAvailableAsync(
        string? destination,
        string? tripType,
        DateOnly? travelDateFrom,
        DateOnly? travelDateTo,
        decimal? minBudget,
        decimal? maxBudget,
        string? sortBy)
    {
        var query = GetOpenTripRequests();

        if (!string.IsNullOrWhiteSpace(destination))
        {
            var destinationFilter = destination.Trim().ToLower();
            query = query.Where(request =>
                request.Destination.ToLower().Contains(destinationFilter));
        }

        if (!string.IsNullOrWhiteSpace(tripType))
        {
            var tripTypeFilter = tripType.Trim().ToLower();
            query = query.Where(request =>
                request.TripType.ToLower() == tripTypeFilter);
        }

        if (travelDateFrom.HasValue)
        {
            query = query.Where(request =>
                request.EndDate >= travelDateFrom.Value);
        }

        if (travelDateTo.HasValue)
        {
            query = query.Where(request =>
                request.StartDate <= travelDateTo.Value);
        }

        if (minBudget.HasValue)
        {
            query = query.Where(request => request.Budget >= minBudget.Value);
        }

        if (maxBudget.HasValue)
        {
            query = query.Where(request => request.Budget <= maxBudget.Value);
        }

        query = sortBy?.ToLowerInvariant() switch
        {
            "budget-high-to-low" => query.OrderByDescending(request => request.Budget)
                .ThenByDescending(request => request.CreatedAt),
            "budget-low-to-high" => query.OrderBy(request => request.Budget)
                .ThenByDescending(request => request.CreatedAt),
            _ => query.OrderByDescending(request => request.CreatedAt)
        };

        return await query.ToListAsync();
    }

    public async Task<AvailableTripRequestDto?> GetAvailableByIdAsync(
        int tripRequestId)
    {
        return await GetOpenTripRequests()
            .FirstOrDefaultAsync(request => request.Id == tripRequestId);
    }

    public async Task<VerificationStatus?> GetGuideVerificationStatusAsync(
        int guideUserId)
    {
        return await _context.GuideProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == guideUserId)
            .Select(profile => (VerificationStatus?)profile.VerificationStatus)
            .FirstOrDefaultAsync();
    }

    private IQueryable<AvailableTripRequestDto> GetOpenTripRequests()
    {
        return from request in _context.TripRequests.AsNoTracking()
            where request.Status == TripRequestStatus.Open
            select new AvailableTripRequestDto
            {
                Id = request.Id,
                Destination = request.Destination,
                TripType = request.TripType,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                NumberOfTravelers = request.NumberOfTravelers,
                Budget = request.Budget,
                Description = request.Description,
                TravelPreferences = request.TravelPreferences,
                Status = "Open",
                CreatedAt = request.CreatedAt
            };
    }

    private static TripRequestResponseDto ToResponse(
        TripRequest tripRequest)
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
            Description = tripRequest.Description,
            TravelPreferences = tripRequest.TravelPreferences,
            Status = tripRequest.Status.ToString(),
            CreatedAt = tripRequest.CreatedAt,
            UpdatedAt = tripRequest.UpdatedAt
        };
    }
}

