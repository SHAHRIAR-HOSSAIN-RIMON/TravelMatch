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
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<RegisterResultDto> RegisterAsync(RegisterRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

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
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber.Trim(),
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

    public async Task<LoginResultDto> LoginAsync(LoginRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return new LoginResultDto
            {
                Success = false,
                Error = LoginError.InvalidCredentials,
                Message = "Invalid email or password."
            };
        }

        if (!user.IsActive)
        {
            return new LoginResultDto
            {
                Success = false,
                Error = LoginError.AccountInactive,
                Message = "This account is inactive."
            };
        }

        return new LoginResultDto
        {
            Success = true,
            Message = "Login completed successfully.",
            Data = new LoginResponseDto
            {
                Token = _jwtTokenService.GenerateToken(user),
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString()
            }
        };
    }
    public async Task<LoginResponseDto?> GetCurrentUserAsync(int userId)
{
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (user is null)
    {
        return null;
    }

    return new LoginResponseDto
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.ToString()
    };
}
}
