using TravelMatch.API.DTOs.Profile;

namespace TravelMatch.API.Interfaces;

public interface IProfileService
{
    /// <summary>
    /// Returns the profile of the authenticated user only. The caller's user ID must
    /// come from the validated JWT, never from the request.
    /// </summary>
    Task<ProfileResultDto> GetMyProfileAsync(int userId);

    /// <summary>
    /// Updates the profile of the authenticated user only, applying only fields
    /// applicable to that user's role.
    /// </summary>
    Task<ProfileResultDto> UpdateMyProfileAsync(int userId, UpdateProfileRequestDto request);
}
