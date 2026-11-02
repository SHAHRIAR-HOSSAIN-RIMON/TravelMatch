using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;

namespace TravelMatch.API.Services;

public static class ActiveAccountValidation
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(id, out var userId))
        {
            context.Fail("Invalid account.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.Role })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (user is null || user.Role.ToString() != context.Principal?.FindFirstValue(ClaimTypes.Role))
            context.Fail("This session is no longer valid. Please log in again.");
    }
}
