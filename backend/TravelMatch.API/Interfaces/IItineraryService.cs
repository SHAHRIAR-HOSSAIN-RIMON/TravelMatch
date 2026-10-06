using TravelMatch.API.DTOs.Itineraries;

namespace TravelMatch.API.Interfaces;

public interface IItineraryService
{
    Task<ProposalResultDto> GetProposalAsync(
        int guideUserId,
        int proposalId);

    Task<ProposalResultDto> CreateProposalAsync(
        int guideUserId,
        int tripRequestId,
        CreateProposalDto request);

    Task<ItineraryDayListResultDto> GetItineraryAsync(
        int guideUserId,
        int proposalId);

    Task<ItineraryResultDto> AddDayAsync(
        int guideUserId,
        int proposalId,
        CreateItineraryDayDto request);

    Task<ItineraryResultDto> UpdateDayAsync(
        int guideUserId,
        int dayId,
        UpdateItineraryDayDto request);

    Task<ItineraryResultDto> DeleteDayAsync(
        int guideUserId,
        int dayId);

    Task<ItineraryResultDto> SaveItineraryAsync(
        int guideUserId,
        int proposalId,
        IEnumerable<CreateItineraryDayDto> days);
}
