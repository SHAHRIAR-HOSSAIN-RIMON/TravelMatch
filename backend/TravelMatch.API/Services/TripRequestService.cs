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
        var validationError = ValidateDatesAndValues(
            request.StartDate,
            request.EndDate,
            request.NumberOfTravelers,
            request.Budget);

        if (validationError is not null)
            return validationError;

        var touristExists = await _context.Users.AnyAsync(u =>
            u.Id == touristId &&
            u.Role == UserRole.Tourist &&
            u.IsActive);

        if (!touristExists)
        {
            return Fail(
                TripRequestError.Unauthorized,
                "Only active tourist accounts can create trip requests.");
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

        return Success(
            "Trip request created successfully.",
            ToResponse(tripRequest));
    }

    public async Task<TripRequestResultDto> GetMyTripRequestAsync(
        int touristId,
        int tripRequestId)
    {
        var tripRequest = await FindOwnedTripRequestAsync(
            touristId,
            tripRequestId);

        if (tripRequest is null)
        {
            return Fail(
                TripRequestError.TripRequestNotFound,
                "Trip request was not found.");
        }

        return Success(
            "Trip request retrieved successfully.",
            ToResponse(tripRequest));
    }

    public async Task<TripRequestResultDto> UpdateMyTripRequestAsync(
        int touristId,
        int tripRequestId,
        UpdateTripRequestDto request)
    {
        var tripRequest = await FindOwnedTripRequestAsync(
            touristId,
            tripRequestId);

        if (tripRequest is null)
        {
            return Fail(
                TripRequestError.TripRequestNotFound,
                "Trip request was not found.");
        }

        if (tripRequest.Status != TripRequestStatus.Open)
        {
            return Fail(
                TripRequestError.InvalidStatus,
                "Only open trip requests can be updated.");
        }

        var validationError = ValidateDatesAndValues(
            request.StartDate,
            request.EndDate,
            request.NumberOfTravelers,
            request.Budget);

        if (validationError is not null)
            return validationError;

        if (string.IsNullOrWhiteSpace(request.Destination))
        {
            return Fail(
                TripRequestError.InvalidDestination,
                "Destination is required.");
        }

        tripRequest.Destination = request.Destination.Trim();
        tripRequest.StartDate = request.StartDate;
        tripRequest.EndDate = request.EndDate;
        tripRequest.NumberOfTravelers = request.NumberOfTravelers;
        tripRequest.Budget = request.Budget;
        tripRequest.Description = request.Description?.Trim() ?? string.Empty;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Success(
            "Trip request updated successfully.",
            ToResponse(tripRequest));
    }

    public async Task<TripRequestResultDto> CancelMyTripRequestAsync(
        int touristId,
        int tripRequestId)
    {
        var tripRequest = await FindOwnedTripRequestAsync(
            touristId,
            tripRequestId);

        if (tripRequest is null)
        {
            return Fail(
                TripRequestError.TripRequestNotFound,
                "Trip request was not found.");
        }

        if (tripRequest.Status != TripRequestStatus.Open)
        {
            return Fail(
                TripRequestError.InvalidStatus,
                "Only open trip requests can be cancelled.");
        }

        tripRequest.Status = TripRequestStatus.Cancelled;
        tripRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Success(
            "Trip request cancelled successfully.",
            ToResponse(tripRequest));
    }

    private async Task<TripRequest?> FindOwnedTripRequestAsync(
        int touristId,
        int tripRequestId)
    {
        return await _context.TripRequests.FirstOrDefaultAsync(t =>
            t.Id == tripRequestId &&
            t.TouristId == touristId);
    }

    private static TripRequestResultDto? ValidateDatesAndValues(
        DateOnly startDate,
        DateOnly endDate,
        int numberOfTravelers,
        decimal budget)
    {
        if (startDate == DateOnly.MinValue)
            return Fail(TripRequestError.InvalidStartDate, "Start date is required.");

        if (endDate == DateOnly.MinValue)
            return Fail(TripRequestError.InvalidEndDate, "End date is required.");

        var bangladeshTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

        var bangladeshToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                bangladeshTimeZone));

        if (startDate < bangladeshToday)
            return Fail(
                TripRequestError.StartDateInPast,
                "Start date cannot be in the past.");

        if (endDate < startDate)
            return Fail(
                TripRequestError.EndDateBeforeStartDate,
                "End date cannot be before start date.");

        if (numberOfTravelers < 1)
            return Fail(
                TripRequestError.InvalidNumberOfTravelers,
                "Number of travelers must be at least 1.");

        if (budget <= 0)
            return Fail(
                TripRequestError.InvalidBudget,
                "Budget must be greater than zero.");

        return null;
    }

    private static TripRequestResultDto Success(
        string message,
        TripRequestResponseDto data)
    {
        return new TripRequestResultDto
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    private static TripRequestResultDto Fail(
        TripRequestError error,
        string message)
    {
        return new TripRequestResultDto
        {
            Success = false,
            Error = error,
            Message = message
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
