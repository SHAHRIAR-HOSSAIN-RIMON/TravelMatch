using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.Models;

namespace TravelMatch.API.Interfaces;

public interface IDisputeService
{
    Task<DisputeResultDto> RaiseDisputeAsync(int userId, UserRole userRole, RaiseDisputeDto request);

    /// <summary>
    /// Admin-only resolution. Moves an Open dispute to Resolved and records the resolving
    /// admin, note and timestamp. Resolving a dispute that is no longer Open is rejected.
    /// </summary>
    Task<DisputeResultDto> ResolveDisputeAsync(int adminUserId, int disputeId, string? resolutionNote);
}
