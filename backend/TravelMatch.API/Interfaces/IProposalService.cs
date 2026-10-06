using TravelMatch.API.DTOs.Proposals;

namespace TravelMatch.API.Interfaces;

public interface IProposalService
{
    Task<ProposalListResultDto> GetProposalsAsync(
        int touristUserId,
        int tripRequestId,
        ProposalSort sort);

    Task<ProposalDetailResultDto> GetProposalDetailAsync(
        int touristUserId,
        int tripRequestId,
        int proposalId);

    Task<AcceptProposalResultDto> AcceptProposalAsync(
        int touristUserId,
        int tripRequestId,
        int proposalId);
}
