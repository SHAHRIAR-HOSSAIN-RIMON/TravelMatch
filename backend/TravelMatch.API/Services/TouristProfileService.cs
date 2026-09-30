using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Tourist;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class TouristProfileService : ITouristProfileService
{
    private readonly ApplicationDbContext _context;

    public TouristProfileService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TouristProfileResponseDto?> GetMyProfileAsync(int userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.Role == UserRole.Tourist &&
                u.IsActive);

        if (user is null)
        {
            return null;
        }

        var profile = await _context.TouristProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            return null;
        }

        return ToResponse(user, profile);
    }

    public async Task<(bool Success, string? Error, TouristProfileResponseDto? Data)> UpdateMyProfileAsync(
        int userId,
        UpdateTouristProfileDto request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.Role == UserRole.Tourist &&
                u.IsActive);

        if (user is null)
        {
            return (false, "Tourist account was not found.", null);
        }

        var profile = await _context.TouristProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            return (false, "Tourist profile was not found.", null);
        }

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var phoneNumber = request.PhoneNumber.Trim();
        var preferences = request.Preferences?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (false, "Full name is required.", null);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "Email is required.", null);
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return (false, "Phone number is required.", null);
        }

        var emailAlreadyUsed = await _context.Users.AnyAsync(u =>
            u.Id != userId &&
            u.Email.ToLower() == email);

        if (emailAlreadyUsed)
        {
            return (false, "Email is already registered by another user.", null);
        }

        user.FullName = fullName;
        user.Email = email;
        user.PhoneNumber = phoneNumber;
        profile.Preferences = preferences;

        await _context.SaveChangesAsync();

        return (true, null, ToResponse(user, profile));
    }

    private static TouristProfileResponseDto ToResponse(
        User user,
        TouristProfile profile)
    {
        return new TouristProfileResponseDto
        {
            UserId = user.Id,
            ProfileId = profile.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Preferences = profile.Preferences
        };
    }
}
