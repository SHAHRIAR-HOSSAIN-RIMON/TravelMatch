using TravelMatch.API.DTOs.OrganizedTrips;

namespace TravelMatch.API.Interfaces;

public interface IOrganizedTripService
{
    Task<OrganizedTripResultDto> CreateAsync(int userId, CreateOrganizedTripDto request);

    Task<OrganizedTripResultDto> PublishAsync(int userId, int organizedTripId);
}
