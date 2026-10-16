using TravelMatch.API.DTOs.Proposals;

namespace TravelMatch.API.Interfaces;

public interface IProposalService
{
    // Tourist-facing
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

    // Guide-facing
    Task<SubmitProposalResultDto> SubmitProposalAsync(
        int guideUserId,
        int tripRequestId,
        int proposalId,
        SubmitProposalDto request);

    Task<UpdateProposalResultDto> UpdateProposalAsync(
        int guideUserId,
        int tripRequestId,
        int proposalId,
        UpdateProposalDto request);

    Task<WithdrawProposalResultDto> WithdrawProposalAsync(
        int guideUserId,
        int tripRequestId,
        int proposalId);

    Task<GuideProposalListResultDto> GetMyProposalsAsync(int guideUserId);
}
