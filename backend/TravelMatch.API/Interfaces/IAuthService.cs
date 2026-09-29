using TravelMatch.API.DTOs.Auth;

namespace TravelMatch.API.Interfaces;

public interface IAuthService
{
   Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request);
    Task<LoginResultDto> LoginAsync(LoginRequestDto request);
  Task<LoginResponseDto?> GetCurrentUserAsync(int userId);
}