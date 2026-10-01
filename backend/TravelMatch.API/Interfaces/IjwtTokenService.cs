using TravelMatch.API.Models;

namespace TravelMatch.API.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}