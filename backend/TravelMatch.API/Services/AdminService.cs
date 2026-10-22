using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Admin;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class AdminService : IAdminService
{
    private const int MaxPageSize = 100;

    private readonly ApplicationDbContext _context;

    public AdminService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardResultDto> GetDashboardAsync(int adminUserId)
    {
        if (!await IsAdminAsync(adminUserId))
            return new AdminDashboardResultDto
            {
                Success = false,
                Error = AdminError.AdminOnly,
                Message = "Only an administrator may view the dashboard."
            };

        var data = new AdminDashboardSummaryDto
        {
            TotalUsers = await _context.Users.CountAsync(),
            TotalTourists = await _context.Users.CountAsync(u => u.Role == UserRole.Tourist),
            TotalGuides = await _context.Users.CountAsync(u => u.Role == UserRole.Guide),
            TotalOrganizers = await _context.Users.CountAsync(u => u.Role == UserRole.Organizer),
            TotalTripRequests = await _context.TripRequests.CountAsync(),
            TotalOrganizedTrips = await _context.OrganizedTrips.CountAsync(),
            TotalRegistrations = await _context.Registrations.CountAsync(),
            TotalProposals = await _context.Proposals.CountAsync(),
            TotalPayments = await _context.Payments.CountAsync(),
            TotalOpenDisputes = await _context.Disputes.CountAsync(d => d.Status == DisputeStatus.Open),
            TotalPendingGuideVerifications = await _context.GuideProfiles
                .CountAsync(gp => gp.VerificationStatus == VerificationStatus.Pending)
        };

        return new AdminDashboardResultDto
        {
            Success = true,
            Message = "Dashboard summary retrieved successfully.",
            Data = data
        };
    }

    public async Task<AdminUserListResultDto> GetUsersAsync(
        int adminUserId,
        UserRole? role,
        bool? isActive,
        int page = 1,
        int pageSize = 20)
    {
        if (!await IsAdminAsync(adminUserId))
            return new AdminUserListResultDto
            {
                Success = false,
                Error = AdminError.AdminOnly,
                Message = "Only an administrator may list users."
            };

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            return new AdminUserListResultDto
            {
                Success = false,
                Error = AdminError.InvalidInput,
                Message = $"page must be at least 1 and pageSize must be between 1 and {MaxPageSize}."
            };

        var query = _context.Users.AsNoTracking().AsQueryable();

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return new AdminUserListResultDto
        {
            Success = true,
            Message = "Users retrieved successfully.",
            Data = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<AdminUserStatusResultDto> SetUserStatusAsync(
        int adminUserId,
        int userId,
        bool isActive)
    {
        if (!await IsAdminAsync(adminUserId))
            return new AdminUserStatusResultDto
            {
                Success = false,
                Error = AdminError.AdminOnly,
                Message = "Only an administrator may change user status."
            };

        if (userId == adminUserId && !isActive)
            return new AdminUserStatusResultDto
            {
                Success = false,
                Error = AdminError.CannotDeactivateSelf,
                Message = "Administrators cannot deactivate their own account."
            };

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return new AdminUserStatusResultDto
            {
                Success = false,
                Error = AdminError.UserNotFound,
                Message = "User not found."
            };

        user.IsActive = isActive;
        await _context.SaveChangesAsync();

        return new AdminUserStatusResultDto
        {
            Success = true,
            Message = isActive
                ? "User activated successfully."
                : "User deactivated successfully.",
            Data = ToDto(user)
        };
    }

    private Task<bool> IsAdminAsync(int userId) =>
        _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin);

    private static AdminUserDto ToDto(User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}
