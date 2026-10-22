using TravelMatch.API.DTOs.Admin;
using TravelMatch.API.Models;

namespace TravelMatch.API.Interfaces;

public interface IAdminService
{
    /// <summary>
    /// Returns aggregate platform counts for the admin dashboard.
    /// The service re-validates that <paramref name="adminUserId"/> belongs to an Admin.
    /// </summary>
    Task<AdminDashboardResultDto> GetDashboardAsync(int adminUserId);

    /// <summary>
    /// Returns a paged, optionally filtered list of users.
    /// The service re-validates that <paramref name="adminUserId"/> belongs to an Admin.
    /// </summary>
    Task<AdminUserListResultDto> GetUsersAsync(
        int adminUserId,
        UserRole? role,
        bool? isActive,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Activates or deactivates a user. An admin cannot deactivate their own account.
    /// The service re-validates that <paramref name="adminUserId"/> belongs to an Admin.
    /// </summary>
    Task<AdminUserStatusResultDto> SetUserStatusAsync(
        int adminUserId,
        int userId,
        bool isActive);
}
