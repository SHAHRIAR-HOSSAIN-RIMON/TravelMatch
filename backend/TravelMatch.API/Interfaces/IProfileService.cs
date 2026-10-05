using TravelMatch.API.DTOs.Profiles;

namespace TravelMatch.API.Interfaces;

public interface IProfileService
{
    Task<ProfileResultDto> GetMyProfileAsync(int userId);

    Task<ProfileResultDto> UpdateMyProfileAsync(int userId, UpdateProfileDto request);
}
