
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class TripRequestService : ITripRequestService
{
    private const string NoRequestsMessage =
        "You haven't created any trip request yet.";

    private const string OperationFailedMessage =
        "Couldn't process the trip request.";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<TripRequestService> _logger;

    public TripRequestService(
        ApplicationDbContext context,
        ILogger<TripRequestService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TripRequestResultDto> CreateAsync(
        int touristId,
        CreateTripRequestDto request)
    {
        var input = TripRequestInput.From(request);

        if (!TryValidate(input, out var error, out var message))
        {
            return Failure(error, message);
        }

        var touristExists = await _context.Users.AnyAsync(u =>
            u.Id == touristId &&
            u.Role == UserRole.Tourist &&
            u.IsActive);

        if (!touristExists)
        {
            return Failure(
                TripRequestError.Unauthorized,
                "Only active tourist accounts can create trip requests.");
        }

        var tripRequest = new TripRequest
        {
            TouristId = touristId,
            Destination = input.Destination,
            TripType = input.TripType,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            NumberOfTravelers = input.NumberOfTravelers,
            Budget = input.Budget,
            BudgetMin = input.BudgetMin,
            BudgetMax = input.BudgetMax,
            Description = input.Description,
            TravelPreferences = input.TravelPreferences,
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

    public async Task<MyTripRequestListResultDto> GetMineAsync(
        int touristId,
        MyTripRequestQueryDto query)
    {
        query ??= new MyTripRequestQueryDto();

        query.Page = query.Page < 1
            ? 1
            : Math.Min(query.Page, MyTripRequestQueryDto.MaxPage);

        query.PageSize = query.PageSize < 1
            ? MyTripRequestQueryDto.DefaultPageSize
            : Math.Min(query.PageSize, MyTripRequestQueryDto.MaxPageSize);

        try
        {
            var mine = _context.TripRequests
                .AsNoTracking()
                .Where(t => t.TouristId == touristId);

            if (query.Status.HasValue)
            {
                mine = mine.Where(t => t.Status == query.Status.Value);
            }

            var totalCount = await mine.CountAsync();

            var rows = await mine
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new MyTripRequestListResultDto
            {
                Success = true,
                Error = TripRequestError.None,
                Message = totalCount == 0
                    ? GetEmptyMessage(query)
                    : $"Showing {rows.Count} of {totalCount} trip request(s).",
                Data = rows.Select(ToResponse).ToList(),
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize),
                StatusFilter = query.Status?.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load trip requests of tourist {TouristId}.",
                touristId);

            return new MyTripRequestListResultDto
            {
                Success = false,
                Error = TripRequestError.ServerError,
                Message = OperationFailedMessage,
                Page = query.Page,
                PageSize = query.PageSize,
                StatusFilter = query.Status?.ToString()
            };
        }
    }

    public async Task<TripRequestResultDto> GetByIdAsync(
        int touristId,
        int tripRequestId)
    {
        if (tripRequestId <= 0)
        {
            return NotFound();
        }

        try
        {
            var tripRequest = await _context.TripRequests
                .AsNoTracking()
                .Where(t => t.Id == tripRequestId && t.TouristId == touristId)
                .FirstOrDefaultAsync();

            if (tripRequest is null)
            {
                return NotFound();
            }

            return new TripRequestResultDto
            {
                Success = true,
                Error = TripRequestError.None,
                Message = "Trip request loaded successfully.",
                Data = ToResponse(tripRequest)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load trip request {TripRequestId} of tourist {TouristId}.",
                tripRequestId,
                touristId);

            return Failure(
                TripRequestError.ServerError,
                OperationFailedMessage);
        }
    }

    public async Task<TripRequestResultDto> UpdateAsync(
        int touristId,
        int tripRequestId,
        UpdateTripRequestDto request)
    {
        if (tripRequestId <= 0)
        {
            return NotFound();
        }

        var input = TripRequestInput.From(request);

        if (!TryValidate(input, out var error, out var message))
        {
            return Failure(error, message);
        }

        try
        {
            var tripRequest = await _context.TripRequests
                .Where(t => t.Id == tripRequestId && t.TouristId == touristId)
                .FirstOrDefaultAsync();

            if (tripRequest is null)
            {
                return NotFound();
            }

            if (tripRequest.Status != TripRequestStatus.Open)
            {
                return Failure(
                    TripRequestError.TripRequestNotOpen,
                    "Only open trip requests can be updated.");
            }

            tripRequest.Destination = input.Destination;
            tripRequest.TripType = input.TripType;
            tripRequest.StartDate = input.StartDate;
            tripRequest.EndDate = input.EndDate;
            tripRequest.NumberOfTravelers = input.NumberOfTravelers;
            tripRequest.Budget = input.Budget;
            tripRequest.BudgetMin = input.BudgetMin;
            tripRequest.BudgetMax = input.BudgetMax;
            tripRequest.Description = input.Description;
            tripRequest.TravelPreferences = input.TravelPreferences;
            tripRequest.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new TripRequestResultDto
            {
                Success = true,
                Error = TripRequestError.None,
                Message = "Trip request updated successfully.",
                Data = ToResponse(tripRequest)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update trip request {TripRequestId} of tourist {TouristId}.",
                tripRequestId,
                touristId);

            return Failure(
                TripRequestError.ServerError,
                OperationFailedMessage);
        }
    }

    public async Task<TripRequestResultDto> CancelAsync(
        int touristId,
        int tripRequestId)
    {
        if (tripRequestId <= 0)
        {
            return NotFound();
        }

        try
        {
            var tripRequest = await _context.TripRequests
                .Where(t => t.Id == tripRequestId && t.TouristId == touristId)
                .FirstOrDefaultAsync();

            if (tripRequest is null)
            {
                return NotFound();
            }

            if (tripRequest.Status != TripRequestStatus.Open)
            {
                return Failure(
                    TripRequestError.TripRequestNotOpen,
                    "Only open trip requests can be cancelled.");
            }

            tripRequest.Status = TripRequestStatus.Cancelled;
            tripRequest.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new TripRequestResultDto
            {
                Success = true,
                Error = TripRequestError.None,
                Message = "Trip request cancelled successfully.",
                Data = ToResponse(tripRequest)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to cancel trip request {TripRequestId} of tourist {TouristId}.",
                tripRequestId,
                touristId);

            return Failure(
                TripRequestError.ServerError,
                OperationFailedMessage);
        }
    }

    private static string GetEmptyMessage(MyTripRequestQueryDto query)
    {
        return query.Status.HasValue
            ? $"You have no {query.Status.Value.ToString().ToLowerInvariant()} trip request."
            : NoRequestsMessage;
    }

    private static TripRequestResultDto NotFound()
    {
        return Failure(
            TripRequestError.TripRequestNotFound,
            "Trip request not found.");
    }

    private static TripRequestResultDto Failure(
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

    private static bool TryValidate(
        TripRequestInput input,
        out TripRequestError error,
        out string message)
    {
        error = TripRequestError.None;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(input.Destination))
        {
            error = TripRequestError.InvalidDestination;
            message = "Destination is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(input.TripType))
        {
            error = TripRequestError.InvalidTripType;
            message = "Trip type is required.";
            return false;
        }

        if (input.StartDate == DateOnly.MinValue)
        {
            error = TripRequestError.InvalidStartDate;
            message = "Start date is required.";
            return false;
        }

        if (input.EndDate == DateOnly.MinValue)
        {
            error = TripRequestError.InvalidEndDate;
            message = "End date is required.";
            return false;
        }

        var bangladeshTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

        var bangladeshToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                bangladeshTimeZone));

        if (input.StartDate < bangladeshToday)
        {
            error = TripRequestError.StartDateInPast;
            message = "Start date cannot be in the past.";
            return false;
        }

        if (input.EndDate < input.StartDate)
        {
            error = TripRequestError.EndDateBeforeStartDate;
            message = "End date cannot be before start date.";
            return false;
        }

        if (input.NumberOfTravelers < 1)
        {
            error = TripRequestError.InvalidNumberOfTravelers;
            message = "Number of travelers must be at least 1.";
            return false;
        }

        if (input.Budget <= 0)
        {
            error = TripRequestError.InvalidBudget;
            message = "Budget must be greater than zero.";
            return false;
        }

        if (!input.HasBudgetRange ||
            input.BudgetMin > input.BudgetMax)
        {
            error = TripRequestError.InvalidBudget;
            message = "Provide both budget limits and ensure the minimum does not exceed the maximum.";
            return false;
        }

        return true;
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
            BudgetMin = tripRequest.BudgetMin,
            BudgetMax = tripRequest.BudgetMax,
            Description = tripRequest.Description,
            TravelPreferences = tripRequest.TravelPreferences,
            Status = tripRequest.Status.ToString(),
            CreatedAt = tripRequest.CreatedAt,
            UpdatedAt = tripRequest.UpdatedAt
        };
    }

    private sealed class TripRequestInput
    {
        public string Destination { get; init; } = string.Empty;

        public string TripType { get; init; } = string.Empty;

        public DateOnly StartDate { get; init; }

        public DateOnly EndDate { get; init; }

        public int NumberOfTravelers { get; init; }

        public decimal Budget { get; init; }

        public decimal BudgetMin { get; init; }

        public decimal BudgetMax { get; init; }

        public bool HasBudgetRange { get; init; }

        public string Description { get; init; } = string.Empty;

        public string TravelPreferences { get; init; } = string.Empty;

        public static TripRequestInput From(CreateTripRequestDto request)
        {
            var hasBudgetRange = request.BudgetMin.HasValue && request.BudgetMax.HasValue;

            return new TripRequestInput
            {
                Destination = request.Destination?.Trim() ?? string.Empty,
                TripType = request.TripType?.Trim() ?? string.Empty,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                NumberOfTravelers = request.NumberOfTravelers,
                Budget = request.Budget,
                BudgetMin = request.BudgetMin ?? request.Budget,
                BudgetMax = request.BudgetMax ?? request.Budget,
                HasBudgetRange = hasBudgetRange,
                Description = request.Description?.Trim() ?? string.Empty,
                TravelPreferences = request.TravelPreferences?.Trim() ?? string.Empty
            };
        }

        public static TripRequestInput From(UpdateTripRequestDto request)
        {
            var hasBudgetRange = request.BudgetMin.HasValue && request.BudgetMax.HasValue;

            return new TripRequestInput
            {
                Destination = request.Destination?.Trim() ?? string.Empty,
                TripType = request.TripType?.Trim() ?? string.Empty,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                NumberOfTravelers = request.NumberOfTravelers,
                Budget = request.Budget,
                BudgetMin = request.BudgetMin ?? request.Budget,
                BudgetMax = request.BudgetMax ?? request.Budget,
                HasBudgetRange = hasBudgetRange,
                Description = request.Description?.Trim() ?? string.Empty,
                TravelPreferences = request.TravelPreferences?.Trim() ?? string.Empty
            };
        }
    }
}
