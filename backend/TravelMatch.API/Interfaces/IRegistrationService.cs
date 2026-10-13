using TravelMatch.API.DTOs.Registrations;

namespace TravelMatch.API.Interfaces;

public interface IRegistrationService
{
    Task<RegistrationResultDto> RegisterAsync(int touristId, int organizedTripId, CreateRegistrationDto request);

    Task<RegistrationResultDto> ConfirmPaymentAsync(int touristId, int registrationId);

    Task<RegistrationResultDto> CancelRegistrationAsync(int touristId, int registrationId, string? reason);
}