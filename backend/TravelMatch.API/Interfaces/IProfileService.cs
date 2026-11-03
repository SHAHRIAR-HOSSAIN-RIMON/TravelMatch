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

    /// <summary>
    /// Stores a new profile photo URL on the authenticated user's role profile
    /// (Tourist, Guide or Organizer) and returns the previous URL so the caller can
    /// remove the old file.
    /// </summary>
    Task<(ProfileResultDto Result, string? PreviousPhotoUrl)> SetMyPhotoAsync(int userId, string photoUrl);
}
