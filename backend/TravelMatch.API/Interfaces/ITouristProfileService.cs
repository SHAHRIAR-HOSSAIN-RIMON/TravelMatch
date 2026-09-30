using TravelMatch.API.DTOs.Tourist;

namespace TravelMatch.API.Interfaces;

public interface ITouristProfileService
{
    Task<TouristProfileResponseDto?> GetMyProfileAsync(int userId);
    Task<(bool Success, string? Error, TouristProfileResponseDto? Data)> UpdateMyProfileAsync(
        int userId,
        UpdateTouristProfileDto request);
}
