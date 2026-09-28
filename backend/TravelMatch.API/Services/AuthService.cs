using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Auth;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser is not null)
        {
            return new RegisterResultDto
            {
                Success = false,
                Error = RegisterError.DuplicateEmail,
                Message = "Email is already registered."
            };
        }
        if (request.Role == UserRole.Admin)
    {
        return new RegisterResultDto
        {
            Success = false,
            Error = RegisterError.InvalidRole,
            Message = "Admin accounts cannot be created through registration."
        };
    }

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = request.Role
        };
await using var transaction = await _context.Database.BeginTransactionAsync();

_context.Users.Add(user);
await _context.SaveChangesAsync();

switch (user.Role)
{
    case UserRole.Tourist:
        _context.TouristProfiles.Add(new TouristProfile { UserId = user.Id });
        break;

    case UserRole.Guide:
        _context.GuideProfiles.Add(new GuideProfile { UserId = user.Id });
        break;

    case UserRole.Organizer:
        _context.OrganizerProfiles.Add(new OrganizerProfile { UserId = user.Id });
        break;
}

await _context.SaveChangesAsync();
await transaction.CommitAsync();
        return new RegisterResultDto
        {
            Success = true,
            Message = "Registration completed successfully.",
            Data = new RegisterResponseDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString()
            }
        };
    }
}
