using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.Models;

namespace TravelMatch.API.Interfaces;

public interface IDisputeService
{
    Task<DisputeResultDto> RaiseDisputeAsync(int userId, UserRole userRole, RaiseDisputeDto request);
}
