using TravelMatch.API.DTOs.ItineraryTracking;

namespace TravelMatch.API.Interfaces;

public interface IItineraryTrackingService
{
    Task<bool> IsProposalParticipantAsync(
        int userId,
        int proposalId);

    Task<ItineraryTrackingListResultDto> GetItineraryTrackingAsync(
        int userId,
        int proposalId);

    Task<ItineraryTrackingResultDto> UpdateActivityStatusAsync(
        int guideUserId,
        int activityId,
        string status);

    Task<ItineraryTrackingResultDto> CreateActivityAsync(
        int guideUserId,
        CreateItineraryActivityDto request);

    Task<ItineraryTrackingResultDto> DeleteActivityAsync(
        int guideUserId,
        int activityId);
}