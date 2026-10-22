using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Admin;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for GET /api/admin/users and PATCH /api/admin/users/{userId}/status.
/// Service tests use EF Core InMemory directly; controller tests exercise claim
/// extraction, validation and HTTP mappings. Framework authorization attributes
/// are verified by reflection because the project has no HTTP-host harness.
/// </summary>
public class AdminUserManagementTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static AdminService BuildService(ApplicationDbContext ctx) => new(ctx);

    private static AdminController BuildController(
        AdminService service, int? userId = 900, string? role = "Admin")
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        return new AdminController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static async Task<User> SeedUserAsync(
        ApplicationDbContext ctx,
        int id,
        UserRole role,
        bool isActive = true)
    {
        var user = new User
        {
            Id = id,
            FullName = $"{role} {id}",
            Email = $"user{id}@t.com",
            PhoneNumber = $"017{id:0000000}",
            Role = role,
            IsActive = isActive,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(id)
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    private static Task<User> SeedAdminAsync(ApplicationDbContext ctx, int userId = 900) =>
        SeedUserAsync(ctx, userId, UserRole.Admin);

    // ── Listing — filters and pagination ─────────────────────────────────────

    [Fact]
    public async Task GetUsers_NoFilters_ReturnsAllUsersOrderedById()
    {
        await using var ctx = BuildContext(nameof(GetUsers_NoFilters_ReturnsAllUsersOrderedById));
        var admin = await SeedAdminAsync(ctx);
        await SeedUserAsync(ctx, 3, UserRole.Guide);
        await SeedUserAsync(ctx, 2, UserRole.Tourist);

        var result = await BuildService(ctx).GetUsersAsync(admin.Id, role: null, isActive: null);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(new[] { 2, 3, 900 }, result.Data.Select(u => u.Id));
    }

    [Fact]
    public async Task GetUsers_FilterByRole_ReturnsOnlyMatchingRole()
    {
        await using var ctx = BuildContext(nameof(GetUsers_FilterByRole_ReturnsOnlyMatchingRole));
        var admin = await SeedAdminAsync(ctx);
        await SeedUserAsync(ctx, 1, UserRole.Tourist);
        await SeedUserAsync(ctx, 2, UserRole.Tourist);
        await SeedUserAsync(ctx, 3, UserRole.Guide);

        var result = await BuildService(ctx).GetUsersAsync(admin.Id, UserRole.Guide, isActive: null);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalCount);
        var user = Assert.Single(result.Data);
        Assert.Equal(3, user.Id);
        Assert.Equal("Guide", user.Role);
    }

    [Fact]
    public async Task GetUsers_FilterByIsActive_ReturnsOnlyMatchingUsers()
    {
        await using var ctx = BuildContext(nameof(GetUsers_FilterByIsActive_ReturnsOnlyMatchingUsers));
        var admin = await SeedAdminAsync(ctx);
        await SeedUserAsync(ctx, 1, UserRole.Tourist, isActive: true);
        await SeedUserAsync(ctx, 2, UserRole.Tourist, isActive: false);

        var inactive = await BuildService(ctx).GetUsersAsync(admin.Id, role: null, isActive: false);
        var active = await BuildService(ctx).GetUsersAsync(admin.Id, role: null, isActive: true);

        Assert.True(inactive.Success);
        Assert.Equal(1, inactive.TotalCount);
        Assert.Equal(2, Assert.Single(inactive.Data).Id);
        Assert.False(inactive.Data[0].IsActive);

        Assert.True(active.Success);
        Assert.Equal(2, active.TotalCount);
        Assert.Equal(new[] { 1, 900 }, active.Data.Select(u => u.Id));
        Assert.All(active.Data, u => Assert.True(u.IsActive));
    }

    [Fact]
    public async Task GetUsers_Pagination_ReturnsRequestedPage()
    {
        await using var ctx = BuildContext(nameof(GetUsers_Pagination_ReturnsRequestedPage));
        var admin = await SeedAdminAsync(ctx);
        for (var id = 1; id <= 5; id++)
            await SeedUserAsync(ctx, id, UserRole.Tourist);

        var page2 = await BuildService(ctx).GetUsersAsync(admin.Id, UserRole.Tourist, null, page: 2, pageSize: 2);
        var page4 = await BuildService(ctx).GetUsersAsync(admin.Id, UserRole.Tourist, null, page: 4, pageSize: 2);

        Assert.True(page2.Success);
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(2, page2.Page);
        Assert.Equal(2, page2.PageSize);
        Assert.Equal(new[] { 3, 4 }, page2.Data.Select(u => u.Id));

        Assert.True(page4.Success);
        Assert.Empty(page4.Data);
        Assert.Equal(5, page4.TotalCount);
    }

    [Fact]
    public async Task GetUsers_InvalidPagination_ReturnsInvalidInput()
    {
        await using var ctx = BuildContext(nameof(GetUsers_InvalidPagination_ReturnsInvalidInput));
        var admin = await SeedAdminAsync(ctx);
        var svc = BuildService(ctx);

        var zeroPage = await svc.GetUsersAsync(admin.Id, null, null, page: 0, pageSize: 20);
        var zeroSize = await svc.GetUsersAsync(admin.Id, null, null, page: 1, pageSize: 0);
        var oversized = await svc.GetUsersAsync(admin.Id, null, null, page: 1, pageSize: 101);

        Assert.False(zeroPage.Success);
        Assert.Equal(AdminError.InvalidInput, zeroPage.Error);
        Assert.False(zeroSize.Success);
        Assert.Equal(AdminError.InvalidInput, zeroSize.Error);
        Assert.False(oversized.Success);
        Assert.Equal(AdminError.InvalidInput, oversized.Error);
    }

    [Fact]
    public async Task GetUsers_ByNonAdmin_ReturnsAdminOnly()
    {
        await using var ctx = BuildContext(nameof(GetUsers_ByNonAdmin_ReturnsAdminOnly));
        await SeedAdminAsync(ctx);
        var tourist = await SeedUserAsync(ctx, 2, UserRole.Tourist);

        var result = await BuildService(ctx).GetUsersAsync(tourist.Id, null, null);

        Assert.False(result.Success);
        Assert.Equal(AdminError.AdminOnly, result.Error);
        Assert.Empty(result.Data);
    }

    // ── Listing — controller validation and mappings ──────────────────────────

    [Fact]
    public async Task GetUsers_ValidRoleCaseInsensitive_Returns200()
    {
        await using var ctx = BuildContext(nameof(GetUsers_ValidRoleCaseInsensitive_Returns200));
        var admin = await SeedAdminAsync(ctx);
        await SeedUserAsync(ctx, 1, UserRole.Tourist);
        await SeedUserAsync(ctx, 2, UserRole.Tourist);
        var controller = BuildController(BuildService(ctx), admin.Id);

        var actionResult = await controller.GetUsers(role: "tourist");

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<AdminUserListResultDto>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal(2, body.TotalCount);
        Assert.All(body.Data, u => Assert.Equal("Tourist", u.Role));
    }

    [Fact]
    public async Task GetUsers_InvalidRole_Returns400()
    {
        await using var ctx = BuildContext(nameof(GetUsers_InvalidRole_Returns400));
        var admin = await SeedAdminAsync(ctx);
        var controller = BuildController(BuildService(ctx), admin.Id);

        var unknownRole = await controller.GetUsers(role: "Wizard");
        var numericRole = await controller.GetUsers(role: "0");

        var unknownBadRequest = Assert.IsType<BadRequestObjectResult>(unknownRole);
        var unknownBody = Assert.IsType<AdminUserListResultDto>(unknownBadRequest.Value);
        Assert.False(unknownBody.Success);
        Assert.Equal(AdminError.InvalidInput, unknownBody.Error);

        var numericBadRequest = Assert.IsType<BadRequestObjectResult>(numericRole);
        var numericBody = Assert.IsType<AdminUserListResultDto>(numericBadRequest.Value);
        Assert.False(numericBody.Success);
        Assert.Equal(AdminError.InvalidInput, numericBody.Error);
    }

    [Fact]
    public async Task GetUsers_InvalidPaginationQueryParameters_Return400()
    {
        await using var ctx = BuildContext(nameof(GetUsers_InvalidPaginationQueryParameters_Return400));
        var admin = await SeedAdminAsync(ctx);
        var controller = BuildController(BuildService(ctx), admin.Id);

        var zeroPage = await controller.GetUsers(page: 0);
        var zeroSize = await controller.GetUsers(pageSize: 0);
        var oversized = await controller.GetUsers(pageSize: 101);

        foreach (var actionResult in new[] { zeroPage, zeroSize, oversized })
        {
            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
            var body = Assert.IsType<AdminUserListResultDto>(badRequest.Value);
            Assert.False(body.Success);
            Assert.Equal(AdminError.InvalidInput, body.Error);
        }
    }

    [Fact]
    public async Task GetUsers_NonAdminCaller_Returns403()
    {
        await using var ctx = BuildContext(nameof(GetUsers_NonAdminCaller_Returns403));
        await SeedAdminAsync(ctx);
        var tourist = await SeedUserAsync(ctx, 2, UserRole.Tourist);
        var controller = BuildController(BuildService(ctx), tourist.Id, role: "Tourist");

        var actionResult = await controller.GetUsers();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var body = Assert.IsType<AdminUserListResultDto>(objectResult.Value);
        Assert.Equal(AdminError.AdminOnly, body.Error);
    }

    [Fact]
    public async Task GetUsers_MissingNameIdentifierClaim_Returns401()
    {
        await using var ctx = BuildContext(nameof(GetUsers_MissingNameIdentifierClaim_Returns401));
        var controller = BuildController(BuildService(ctx), userId: null);

        var actionResult = await controller.GetUsers();

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
    }

    // ── Status update — service ───────────────────────────────────────────────

    [Fact]
    public async Task SetUserStatus_Deactivate_UpdatesIsActiveAndKeepsRole()
    {
        await using var ctx = BuildContext(nameof(SetUserStatus_Deactivate_UpdatesIsActiveAndKeepsRole));
        var admin = await SeedAdminAsync(ctx);
        var tourist = await SeedUserAsync(ctx, 2, UserRole.Tourist);

        var result = await BuildService(ctx).SetUserStatusAsync(admin.Id, tourist.Id, isActive: false);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.IsActive);
        Assert.Equal("Tourist", result.Data.Role);

        var saved = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == tourist.Id);
        Assert.False(saved.IsActive);
        Assert.Equal(UserRole.Tourist, saved.Role);
    }

    [Fact]
    public async Task SetUserStatus_Activate_UpdatesIsActive()
    {
        await using var ctx = BuildContext(nameof(SetUserStatus_Activate_UpdatesIsActive));
        var admin = await SeedAdminAsync(ctx);
        var tourist = await SeedUserAsync(ctx, 2, UserRole.Tourist, isActive: false);

        var result = await BuildService(ctx).SetUserStatusAsync(admin.Id, tourist.Id, isActive: true);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsActive);
        var saved = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == tourist.Id);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task SetUserStatus_MissingUser_ReturnsUserNotFound()
    {
        await using var ctx = BuildContext(nameof(SetUserStatus_MissingUser_ReturnsUserNotFound));
        var admin = await SeedAdminAsync(ctx);

        var result = await BuildService(ctx).SetUserStatusAsync(admin.Id, 9999, isActive: false);

        Assert.False(result.Success);
        Assert.Equal(AdminError.UserNotFound, result.Error);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task SetUserStatus_SelfDeactivation_IsBlockedAndAdminStaysActive()
    {
        await using var ctx = BuildContext(nameof(SetUserStatus_SelfDeactivation_IsBlockedAndAdminStaysActive));
        var admin = await SeedAdminAsync(ctx);

        var result = await BuildService(ctx).SetUserStatusAsync(admin.Id, admin.Id, isActive: false);

        Assert.False(result.Success);
        Assert.Equal(AdminError.CannotDeactivateSelf, result.Error);
        var saved = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task SetUserStatus_ByNonAdmin_ReturnsAdminOnly()
    {
        await using var ctx = BuildContext(nameof(SetUserStatus_ByNonAdmin_ReturnsAdminOnly));
        await SeedAdminAsync(ctx);
        var actor = await SeedUserAsync(ctx, 2, UserRole.Tourist);
        var target = await SeedUserAsync(ctx, 3, UserRole.Guide);

        var result = await BuildService(ctx).SetUserStatusAsync(actor.Id, target.Id, isActive: false);

        Assert.False(result.Success);
        Assert.Equal(AdminError.AdminOnly, result.Error);
        var saved = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.True(saved.IsActive);
    }

    // ── Status update — controller mappings ───────────────────────────────────

    [Fact]
    public async Task UpdateUserStatus_ValidRequest_Returns200AndDeactivatesUser()
    {
        await using var ctx = BuildContext(nameof(UpdateUserStatus_ValidRequest_Returns200AndDeactivatesUser));
        var admin = await SeedAdminAsync(ctx);
        var tourist = await SeedUserAsync(ctx, 2, UserRole.Tourist);
        var controller = BuildController(BuildService(ctx), admin.Id);

        var actionResult = await controller.UpdateUserStatus(
            tourist.Id, new UpdateUserStatusDto { IsActive = false });

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<AdminUserStatusResultDto>(ok.Value);
        Assert.True(body.Success);
        Assert.False(body.Data!.IsActive);
        Assert.False((await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == tourist.Id)).IsActive);
    }

    [Fact]
    public async Task UpdateUserStatus_MissingUser_Returns404()
    {
        await using var ctx = BuildContext(nameof(UpdateUserStatus_MissingUser_Returns404));
        var admin = await SeedAdminAsync(ctx);
        var controller = BuildController(BuildService(ctx), admin.Id);

        var actionResult = await controller.UpdateUserStatus(
            9999, new UpdateUserStatusDto { IsActive = false });

        var notFound = Assert.IsType<NotFoundObjectResult>(actionResult);
        var body = Assert.IsType<AdminUserStatusResultDto>(notFound.Value);
        Assert.Equal(AdminError.UserNotFound, body.Error);
    }

    [Fact]
    public async Task UpdateUserStatus_SelfDeactivation_Returns400()
    {
        await using var ctx = BuildContext(nameof(UpdateUserStatus_SelfDeactivation_Returns400));
        var admin = await SeedAdminAsync(ctx);
        var controller = BuildController(BuildService(ctx), admin.Id);

        var actionResult = await controller.UpdateUserStatus(
            admin.Id, new UpdateUserStatusDto { IsActive = false });

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var body = Assert.IsType<AdminUserStatusResultDto>(badRequest.Value);
        Assert.Equal(AdminError.CannotDeactivateSelf, body.Error);
        Assert.True((await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id)).IsActive);
    }

    [Fact]
    public async Task UpdateUserStatus_MissingIsActiveField_Returns400()
    {
        await using var ctx = BuildContext(nameof(UpdateUserStatus_MissingIsActiveField_Returns400));
        var admin = await SeedAdminAsync(ctx);
        var controller = BuildController(BuildService(ctx), admin.Id);

        var actionResult = await controller.UpdateUserStatus(
            2, new UpdateUserStatusDto { IsActive = null });

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var body = Assert.IsType<AdminUserStatusResultDto>(badRequest.Value);
        Assert.Equal(AdminError.InvalidInput, body.Error);
    }

    [Fact]
    public async Task UpdateUserStatus_NonAdminCaller_Returns403()
    {
        await using var ctx = BuildContext(nameof(UpdateUserStatus_NonAdminCaller_Returns403));
        await SeedAdminAsync(ctx);
        var actor = await SeedUserAsync(ctx, 2, UserRole.Tourist);
        var target = await SeedUserAsync(ctx, 3, UserRole.Guide);
        var controller = BuildController(BuildService(ctx), actor.Id, role: "Tourist");

        var actionResult = await controller.UpdateUserStatus(
            target.Id, new UpdateUserStatusDto { IsActive = false });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var body = Assert.IsType<AdminUserStatusResultDto>(objectResult.Value);
        Assert.Equal(AdminError.AdminOnly, body.Error);
    }

    [Fact]
    public async Task UpdateUserStatus_MissingNameIdentifierClaim_Returns401()
    {
        await using var ctx = BuildContext(nameof(UpdateUserStatus_MissingNameIdentifierClaim_Returns401));
        var controller = BuildController(BuildService(ctx), userId: null);

        var actionResult = await controller.UpdateUserStatus(
            2, new UpdateUserStatusDto { IsActive = false });

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
    }

    // ── Sensitive fields and endpoint shape ───────────────────────────────────

    [Fact]
    public void AdminUserDto_ExposesOnlyNonSensitiveFields()
    {
        var propertyNames = typeof(AdminUserDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(
            new[] { "CreatedAt", "Email", "FullName", "Id", "IsActive", "Role" },
            propertyNames);
    }

    [Fact]
    public void UpdateUserStatusDto_OnlyExposesIsActive()
    {
        var propertyNames = typeof(UpdateUserStatusDto).GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { "IsActive" }, propertyNames);
    }

    [Fact]
    public void UsersEndpoints_AreAdminOnly_ByAttributes()
    {
        var controllerType = typeof(AdminController);

        var controllerAuthorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(controllerAuthorize);
        Assert.Equal("Admin", controllerAuthorize!.Roles);

        var getUsers = controllerType.GetMethod(nameof(AdminController.GetUsers));
        Assert.NotNull(getUsers);
        var httpGet = getUsers!.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGet);
        Assert.Equal("users", httpGet!.Template);
        Assert.All(
            getUsers.GetParameters(),
            p => Assert.NotNull(p.GetCustomAttribute<FromQueryAttribute>()));

        var updateStatus = controllerType.GetMethod(nameof(AdminController.UpdateUserStatus));
        Assert.NotNull(updateStatus);
        var httpPatch = updateStatus!.GetCustomAttribute<HttpPatchAttribute>();
        Assert.NotNull(httpPatch);
        Assert.Equal("users/{userId:int}/status", httpPatch!.Template);

        var userIdParam = updateStatus.GetParameters().Single(p => p.Name == "userId");
        Assert.NotNull(userIdParam.GetCustomAttribute<FromRouteAttribute>());

        var requestParam = updateStatus.GetParameters().Single(p => p.Name == "request");
        Assert.NotNull(requestParam.GetCustomAttribute<FromBodyAttribute>());
    }
}
