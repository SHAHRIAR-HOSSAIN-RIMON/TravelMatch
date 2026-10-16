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

    Task<RegistrationListResultDto> GetMyRegistrationsAsync(int touristUserId);

    Task<RegistrationResultDto> RegisterAsync(int touristUserId, int organizedTripId, CreateRegistrationDto request);

    Task<RegistrationResultDto> ConfirmPaymentAsync(int touristUserId, int registrationId);

    Task<RegistrationResultDto> CancelRegistrationAsync(int touristUserId, int organizedTripId, int registrationId, string? reason);

    Task<RegistrationResultDto> CancelRegistrationAsOrganizerAsync(
        int organizerUserId,
        int organizedTripId,
        int registrationId,
        string? reason);

    Task<AvailableOrganizedTripListResultDto> GetAvailableTripsAsync();

    Task<AvailableOrganizedTripDetailResultDto> GetAvailableTripDetailAsync(int organizedTripId);
}