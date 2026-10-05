using TravelMatch.API.DTOs.TripRequests;

namespace TravelMatch.API.Interfaces;

public interface ITripRequestService
{
    Task<TripRequestResultDto> CreateAsync(
        int touristId,
        CreateTripRequestDto request);

    Task<MyTripRequestListResultDto> GetMineAsync(
        int touristId,
        MyTripRequestQueryDto query);

    Task<TripRequestResultDto> GetByIdAsync(
        int touristId,
        int tripRequestId);

    Task<TripRequestResultDto> UpdateAsync(
        int touristId,
        int tripRequestId,
        UpdateTripRequestDto request);

    Task<TripRequestResultDto> CancelAsync(
        int touristId,
        int tripRequestId);
}
