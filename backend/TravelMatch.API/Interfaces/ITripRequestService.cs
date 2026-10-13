using TravelMatch.API.DTOs.TripRequests;

namespace TravelMatch.API.Interfaces;

public interface ITripRequestService
{
    Task<TripRequestResultDto> CreateAsync(
        int touristId,
        CreateTripRequestDto request);

    Task<TripRequestResultDto> CancelAsync(
        int touristId,
        int tripRequestId);

    Task<TripRequestLifecycleResultDto> GetLifecycleAsync(int touristId, int tripRequestId);

    Task<TripRequestLifecycleResultDto> TransitionToProposalsReceivedAsync(int touristId, int tripRequestId);

    Task<TripRequestLifecycleResultDto> TransitionToMatchedAsync(int touristId, int tripRequestId, int proposalId);

    Task<TripRequestLifecycleResultDto> TransitionToPaymentPendingAsync(int touristId, int tripRequestId, decimal acceptedPrice);

    Task<TripRequestLifecycleResultDto> TransitionToPlannedAsync(int touristId, int tripRequestId);

    Task<TripRequestLifecycleResultDto> TransitionToInProgressAsync(int touristId, int tripRequestId);

    Task<TripRequestLifecycleResultDto> TransitionToCompletedAsync(int touristId, int tripRequestId);

    Task<TripRequestLifecycleResultDto> TransitionToCancelledAsync(int touristId, int tripRequestId, string? reason);
}