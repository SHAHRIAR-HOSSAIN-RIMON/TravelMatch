
using TravelMatch.API.DTOs.TripRequests;

namespace TravelMatch.API.Interfaces;

public interface ITripRequestService
{
    Task<TripRequestResultDto> CreateAsync(
        int touristId,
        CreateTripRequestDto request);

    Task<IReadOnlyList<AvailableTripRequestDto>> GetAvailableAsync(
        string? destination,
        string? tripType,
        DateOnly? travelDateFrom,
        DateOnly? travelDateTo,
        decimal? minBudget,
        decimal? maxBudget,
        string? sortBy);

    Task<AvailableTripRequestDto?> GetAvailableByIdAsync(int tripRequestId);

    Task<Models.VerificationStatus?> GetGuideVerificationStatusAsync(int guideUserId);
}

