using TravelMatch.API.DTOs.OrganizedTrips;
using TravelMatch.API.DTOs.Registrations;

namespace TravelMatch.API.Interfaces;

public interface IOrganizedTripService
{
    Task<OrganizedTripResultDto> CreateAsync(int userId, CreateOrganizedTripDto request);

    Task<OrganizedTripResultDto> PublishAsync(int userId, int organizedTripId);

    Task<OrganizedTripListResultDto> GetMyTripsAsync(int userId);

    Task<OrganizedTripResultDto> UpdateAsync(int userId, int organizedTripId, CreateOrganizedTripDto request);

    Task<OrganizedTripResultDto> CancelAsync(int userId, int organizedTripId);

    Task<RegistrationListResultDto> GetRegistrationsAsync(int userId, int organizedTripId);

    Task<RegistrationListResultDto> GetMyRegistrationsAsync(int touristId);

    Task<RegistrationResultDto> RegisterAsync(int touristId, int organizedTripId, CreateRegistrationDto request);

    Task<RegistrationResultDto> ConfirmPaymentAsync(int touristId, int registrationId);

    Task<RegistrationResultDto> CancelRegistrationAsync(int touristId, int registrationId, string? reason);
}