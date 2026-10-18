using TravelMatch.API.DTOs.Registrations;

namespace TravelMatch.API.Interfaces;

public interface IRegistrationService
{
    Task<RegistrationResultDto> RegisterAsync(int touristUserId, int organizedTripId, CreateRegistrationDto request);

    Task<RegistrationResultDto> ConfirmPaymentAsync(int touristUserId, int registrationId);

    Task<RegistrationResultDto> CancelRegistrationAsync(
        int touristUserId,
        int organizedTripId,
        int registrationId,
        string? reason);

    Task<RegistrationResultDto> CancelAsOrganizerAsync(
        int organizerUserId,
        int organizedTripId,
        int registrationId,
        string? reason);
}