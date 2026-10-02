using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class GuideTripRequestService : IGuideTripRequestService
{
    private const string NoOpenRequestsMessage =
        "No open trip requests right now. Check back later.";

    private const string NoFilterMatchesMessage =
        "No trip requests match your filters. Try adjusting them.";

    private const string LoadFailedMessage =
        "Couldn't load trip requests.";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<GuideTripRequestService> _logger;

    public GuideTripRequestService(
        ApplicationDbContext context,
        ILogger<GuideTripRequestService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OpenTripRequestListResultDto> GetOpenTripRequestsAsync(
        int guideUserId,
        OpenTripRequestQueryDto query)
    {
        query ??= new OpenTripRequestQueryDto();

        if (!TryNormalizeQuery(query, out var validationError))
        {
            return new OpenTripRequestListResultDto
            {
                Success = false,
                Error = OpenTripRequestError.InvalidFilter,
                Message = validationError
            };
        }

        var hasFiltersApplied = HasFiltersApplied(query);

        try
        {
            var verification = await GetGuideVerificationAsync(guideUserId);

            if (verification is null)
            {
                return new OpenTripRequestListResultDto
                {
                    Success = false,
                    Error = OpenTripRequestError.GuideNotFound,
                    Message = "Guide profile not found."
                };
            }

            var openQuery = _context.TripRequests
                .AsNoTracking()
                .Where(t => t.Status == TripRequestStatus.Open);

            var filteredQuery = ApplyFilters(openQuery, query);

            var totalCount = await filteredQuery.CountAsync();
            var totalOpenCount = hasFiltersApplied
                ? await openQuery.CountAsync()
                : totalCount;

            if (totalCount == 0 && hasFiltersApplied)
            {
                return new OpenTripRequestListResultDto
                {
                    Success = true,
                    Error = OpenTripRequestError.None,
                    Message = NoFilterMatchesMessage,
                    Data = Array.Empty<OpenTripRequestDto>(),
                    TotalCount = 0,
                    TotalOpenCount = totalOpenCount,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalPages = 0,
                    HasFiltersApplied = true,
                    GuideVerificationStatus = verification.Name,
                    CanSubmitProposal = verification.CanSubmitProposal
                };
            }

            var rows = await ApplySorting(filteredQuery, query.Sort)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(t => new OpenTripRequestRow
                {
                    Id = t.Id,
                    Destination = t.Destination,
                    TripType = t.TripType,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    NumberOfTravelers = t.NumberOfTravelers,
                    Budget = t.Budget,
                    BudgetMin = t.BudgetMin,
                    BudgetMax = t.BudgetMax,
                    Description = t.Description,
                    TravelPreferences = t.TravelPreferences,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

            return new OpenTripRequestListResultDto
            {
                Success = true,
                Error = OpenTripRequestError.None,
                Message = totalCount == 0
                    ? NoOpenRequestsMessage
                    : $"Showing {rows.Count} of {totalCount} open trip request(s).",
                Data = rows.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                TotalOpenCount = totalOpenCount,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalPages = totalPages,
                HasFiltersApplied = hasFiltersApplied,
                GuideVerificationStatus = verification.Name,
                CanSubmitProposal = verification.CanSubmitProposal
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load open trip requests for guide {GuideUserId}.",
                guideUserId);

            return new OpenTripRequestListResultDto
            {
                Success = false,
                Error = OpenTripRequestError.ServerError,
                Message = LoadFailedMessage,
                Page = query.Page,
                PageSize = query.PageSize,
                HasFiltersApplied = hasFiltersApplied
            };
        }
    }

    public async Task<OpenTripRequestDetailResultDto> GetOpenTripRequestDetailAsync(
        int guideUserId,
        int tripRequestId)
    {
        if (tripRequestId <= 0)
        {
            return new OpenTripRequestDetailResultDto
            {
                Success = false,
                Error = OpenTripRequestError.TripRequestNotFound,
                Message = "Trip request not found."
            };
        }

        try
        {
            var verification = await GetGuideVerificationAsync(guideUserId);

            if (verification is null)
            {
                return new OpenTripRequestDetailResultDto
                {
                    Success = false,
                    Error = OpenTripRequestError.GuideNotFound,
                    Message = "Guide profile not found."
                };
            }

            var row = await _context.TripRequests
                .AsNoTracking()
                .Where(t => t.Id == tripRequestId)
                .Select(t => new OpenTripRequestRow
                {
                    Id = t.Id,
                    Destination = t.Destination,
                    TripType = t.TripType,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    NumberOfTravelers = t.NumberOfTravelers,
                    Budget = t.Budget,
                    BudgetMin = t.BudgetMin,
                    BudgetMax = t.BudgetMax,
                    Description = t.Description,
                    TravelPreferences = t.TravelPreferences,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (row is null)
            {
                return NotFoundDetail(verification);
            }

            if (row.Status != TripRequestStatus.Open)
            {
                return new OpenTripRequestDetailResultDto
                {
                    Success = false,
                    Error = OpenTripRequestError.TripRequestNotAvailable,
                    Message = "This trip request is no longer available for proposals.",
                    GuideVerificationStatus = verification.Name,
                    CanSubmitProposal = verification.CanSubmitProposal
                };
            }

            return new OpenTripRequestDetailResultDto
            {
                Success = true,
                Error = OpenTripRequestError.None,
                Message = "Trip request loaded successfully.",
                Data = MapToDetailDto(row),
                GuideVerificationStatus = verification.Name,
                CanSubmitProposal = verification.CanSubmitProposal
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load open trip request {TripRequestId} for guide {GuideUserId}.",
                tripRequestId,
                guideUserId);

            return new OpenTripRequestDetailResultDto
            {
                Success = false,
                Error = OpenTripRequestError.ServerError,
                Message = LoadFailedMessage
            };
        }
    }

    private async Task<GuideVerification?> GetGuideVerificationAsync(int guideUserId)
    {
        var guideUserExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u =>
                u.Id == guideUserId &&
                u.Role == UserRole.Guide &&
                u.IsActive);

        if (!guideUserExists)
        {
            return null;
        }

        var status = await _context.GuideProfiles
            .AsNoTracking()
            .Where(p => p.UserId == guideUserId)
            .Select(p => (VerificationStatus?)p.VerificationStatus)
            .FirstOrDefaultAsync()
            ?? VerificationStatus.Pending;

        return new GuideVerification(
            status,
            status == VerificationStatus.Verified);
    }

    private static OpenTripRequestDetailResultDto NotFoundDetail(
        GuideVerification verification)
    {
        return new OpenTripRequestDetailResultDto
        {
            Success = false,
            Error = OpenTripRequestError.TripRequestNotFound,
            Message = "Trip request not found.",
            GuideVerificationStatus = verification.Name,
            CanSubmitProposal = verification.CanSubmitProposal
        };
    }

    private static bool HasFiltersApplied(OpenTripRequestQueryDto query)
    {
        return !string.IsNullOrWhiteSpace(query.Destination) ||
               !string.IsNullOrWhiteSpace(query.TripType) ||
               query.StartDateFrom.HasValue ||
               query.StartDateTo.HasValue ||
               query.MinBudget.HasValue ||
               query.MaxBudget.HasValue;
    }

    private static bool TryNormalizeQuery(
        OpenTripRequestQueryDto query,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (query.StartDateFrom.HasValue &&
            query.StartDateTo.HasValue &&
            query.StartDateFrom.Value > query.StartDateTo.Value)
        {
            errorMessage = "Start date range is invalid.";
            return false;
        }

        if (query.MinBudget.HasValue &&
            query.MaxBudget.HasValue &&
            query.MinBudget.Value > query.MaxBudget.Value)
        {
            errorMessage = "Budget range is invalid.";
            return false;
        }

        if (query.MinBudget < 0)
        {
            errorMessage = "Minimum budget cannot be negative.";
            return false;
        }

        if (query.MaxBudget < 0)
        {
            errorMessage = "Maximum budget cannot be negative.";
            return false;
        }

        query.Destination = string.IsNullOrWhiteSpace(query.Destination)
            ? null
            : query.Destination.Trim();
        query.TripType = string.IsNullOrWhiteSpace(query.TripType)
            ? null
            : query.TripType.Trim();

        query.Page = query.Page < 1
            ? 1
            : Math.Min(query.Page, OpenTripRequestQueryDto.MaxPage);

        query.PageSize = query.PageSize < 1
            ? OpenTripRequestQueryDto.DefaultPageSize
            : Math.Min(query.PageSize, OpenTripRequestQueryDto.MaxPageSize);

        return true;
    }

    private static IQueryable<TripRequest> ApplyFilters(
        IQueryable<TripRequest> query,
        OpenTripRequestQueryDto filter)
    {
        if (filter.Destination is not null)
        {
            var pattern = BuildContainsPattern(filter.Destination);

            query = query.Where(t =>
                EF.Functions.ILike(t.Destination, pattern, "\\"));
        }

        if (filter.TripType is not null)
        {
            var pattern = BuildContainsPattern(filter.TripType);

            query = query.Where(t =>
                EF.Functions.ILike(t.TripType, pattern, "\\"));
        }

        if (filter.StartDateFrom.HasValue)
        {
            var from = filter.StartDateFrom.Value;

            query = query.Where(t => t.StartDate >= from);
        }

        if (filter.StartDateTo.HasValue)
        {
            var to = filter.StartDateTo.Value;

            query = query.Where(t => t.StartDate <= to);
        }

        if (filter.MinBudget.HasValue)
        {
            var min = filter.MinBudget.Value;

            query = query.Where(t => t.BudgetMax >= min);
        }

        if (filter.MaxBudget.HasValue)
        {
            var max = filter.MaxBudget.Value;

            query = query.Where(t => t.BudgetMin <= max);
        }

        return query;
    }

    private static IQueryable<TripRequest> ApplySorting(
        IQueryable<TripRequest> query,
        OpenTripRequestSort sort)
    {
        return sort switch
        {
            OpenTripRequestSort.Oldest =>
                query.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),

            OpenTripRequestSort.BudgetHighToLow =>
                query.OrderByDescending(t => t.BudgetMax)
                    .ThenByDescending(t => t.CreatedAt),

            OpenTripRequestSort.BudgetLowToHigh =>
                query.OrderBy(t => t.BudgetMin)
                    .ThenByDescending(t => t.CreatedAt),

            OpenTripRequestSort.StartDateSoonest =>
                query.OrderBy(t => t.StartDate)
                    .ThenByDescending(t => t.CreatedAt),

            _ =>
                query.OrderByDescending(t => t.CreatedAt)
                    .ThenByDescending(t => t.Id)
        };
    }

    private static string BuildContainsPattern(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }

    private static OpenTripRequestDto MapToDto(OpenTripRequestRow row)
    {
        return new OpenTripRequestDto
        {
            Id = row.Id,
            Destination = row.Destination,
            TripType = row.TripType,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            NumberOfTravelers = row.NumberOfTravelers,
            Budget = row.Budget,
            BudgetMin = row.BudgetMin,
            BudgetMax = row.BudgetMax,
            Description = row.Description,
            TravelPreferences = row.TravelPreferences,
            Status = row.Status.ToString(),
            CreatedAt = row.CreatedAt
        };
    }

    private static OpenTripRequestDetailDto MapToDetailDto(OpenTripRequestRow row)
    {
        return new OpenTripRequestDetailDto
        {
            Id = row.Id,
            Destination = row.Destination,
            TripType = row.TripType,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            NumberOfDays = row.EndDate.DayNumber - row.StartDate.DayNumber + 1,
            NumberOfTravelers = row.NumberOfTravelers,
            Budget = row.Budget,
            BudgetMin = row.BudgetMin,
            BudgetMax = row.BudgetMax,
            Description = row.Description,
            TravelPreferences = row.TravelPreferences,
            Status = row.Status.ToString(),
            CreatedAt = row.CreatedAt
        };
    }

    private sealed record GuideVerification(
        VerificationStatus Status,
        bool CanSubmitProposal)
    {
        public string Name => Status.ToString();
    }

    private sealed class OpenTripRequestRow
    {
        public int Id { get; set; }

        public string Destination { get; set; } = string.Empty;

        public string TripType { get; set; } = string.Empty;

        public DateOnly StartDate { get; set; }

        public DateOnly EndDate { get; set; }

        public int NumberOfTravelers { get; set; }

        public decimal Budget { get; set; }

        public decimal BudgetMin { get; set; }

        public decimal BudgetMax { get; set; }

        public string Description { get; set; } = string.Empty;

        public string TravelPreferences { get; set; } = string.Empty;

        public TripRequestStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}