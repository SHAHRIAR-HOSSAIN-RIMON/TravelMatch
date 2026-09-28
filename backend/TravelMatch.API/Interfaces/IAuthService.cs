using TravelMatch.API.DTOs.Auth;

namespace TravelMatch.API.Interfaces;

public interface IAuthService
{
   Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request);

}