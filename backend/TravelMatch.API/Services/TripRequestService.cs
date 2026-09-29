
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
}

