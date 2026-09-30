using TravelMatch.API.DTOs.TripRequests;

namespace TravelMatch.API.Interfaces;

public interface ITripRequestService
{
    Task<TripRequestResultDto> CreateAsync(
        int touristId,
        CreateTripRequestDto request);

    Task<TripRequestResultDto> GetMyTripRequestAsync(
        int touristId,
        int tripRequestId);

    Task<TripRequestResultDto> UpdateMyTripRequestAsync(
        int touristId,
        int tripRequestId,
        UpdateTripRequestDto request);

    Task<TripRequestResultDto> CancelMyTripRequestAsync(
        int touristId,
        int tripRequestId);
}
