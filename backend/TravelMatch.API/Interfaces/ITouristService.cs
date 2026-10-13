
using TravelMatch.API.DTOs.Tourist;

namespace TravelMatch.API.Interfaces;

public interface ITouristService
{
    Task<TouristProfileResultDto> GetProfileAsync(int userId);

    Task<TouristProfileResultDto> UpdateProfileAsync(
        int userId,
        UpdateTouristProfileRequestDto request);
}
