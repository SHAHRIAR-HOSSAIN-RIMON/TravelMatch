
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.DTOs.Proposals;

namespace TravelMatch.API.Interfaces;

public interface ITripRequestService
{
    Task<TripRequestResultDto> CreateAsync(
        int touristId,
        CreateTripRequestDto request);

    Task<ProposalResultDto> CreateProposalAsync(
        int guideId,
        int tripRequestId,
        CreateProposalDto request);

    Task<IReadOnlyList<ProposalResponseDto>> GetProposalsByGuideAsync(
        int guideId);

    Task<IReadOnlyList<TripRequestResponseDto>> GetOpenTripRequestsAsync();

    Task<TripRequestResponseDto?> GetOpenTripRequestAsync(int tripRequestId);
}

