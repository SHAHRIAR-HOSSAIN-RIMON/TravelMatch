using TravelMatch.API.DTOs.Payments;

namespace TravelMatch.API.Interfaces;

public interface IPaymentService
{
    Task<PaymentResultDto> PayForProposalAsync(int touristUserId, int proposalId, PayProposalRequestDto? request = null);

    Task<PaymentResultDto> PayForRegistrationAsync(int touristUserId, int registrationId, PayRegistrationRequestDto? request = null);
}
