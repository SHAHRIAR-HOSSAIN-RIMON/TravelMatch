
using TravelMatch.API.DTOs.TripRequests;

namespace TravelMatch.API.Interfaces;

public interface ITripRequestService
{
    Task<TripRequestResultDto> CreateAsync(
        int touristId,
        CreateTripRequestDto request);
}

