using TravelMatch.API.DTOs.GuideApplications;

namespace TravelMatch.API.Interfaces;

public interface IGuideApplicationService
{
    Task<OpenOrganizedTripListResultDto> GetAvailableTripsAsync(
        int guideUserId,
        int page = 1,
        int pageSize = 20);

    Task<OpenOrganizedTripDetailResultDto> GetTripDetailAsync(
        int guideUserId,
        int tripId);

    Task<GuideApplicationResultDto> ApplyAsync(
        int guideUserId,
        int tripId,
        CreateGuideApplicationDto request);

    Task<GuideApplicationListResultDto> GetMyApplicationsAsync(
        int guideUserId,
        int page = 1,
        int pageSize = 20);

    Task<GuideApplicationDetailResultDto> GetApplicationAsync(
        int guideUserId,
        int applicationId);

    Task<GuideApplicationListResultDto> GetTripApplicationsAsync(
        int organizerUserId,
        int tripId,
        int page = 1,
        int pageSize = 20);

    Task<SelectGuideApplicationResultDto> SelectGuideApplicationAsync(
        int organizerUserId,
        int tripId,
        int applicationId);
}
