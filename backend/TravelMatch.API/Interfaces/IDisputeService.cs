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

    /// <summary>
    /// Admin-only listing of disputes, optionally filtered by status.
    /// Returns disputes ordered by CreatedAt ascending (oldest first).
    /// </summary>
    Task<DisputeListResultDto> GetDisputesAsync(DisputeStatus? status);

    /// <summary>
    /// Admin-only retrieval of a single dispute by ID.
    /// Returns HTTP 404 if the dispute does not exist.
    /// </summary>
    Task<DisputeResultDto> GetDisputeByIdAsync(int disputeId);
}
