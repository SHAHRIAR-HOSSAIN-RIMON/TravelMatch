namespace TravelMatch.API.Interfaces;

using TravelMatch.API.DTOs.TripRequests;

public interface IGuideTripRequestService
{
    Task<OpenTripRequestListResultDto> GetOpenTripRequestsAsync(
        int guideUserId,
        OpenTripRequestQueryDto query);

    Task<OpenTripRequestDetailResultDto> GetOpenTripRequestDetailAsync(
        int guideUserId,
        int tripRequestId);
}