
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Tourist;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class TouristService : ITouristService
{
    private readonly ApplicationDbContext _context;

    public TouristService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TouristProfileResultDto> GetProfileAsync(int userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null || user.Role != UserRole.Tourist)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.Unauthorized,
                Message = "Only tourist accounts can access a tourist profile."
            };
        }

        if (!user.IsActive)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.AccountInactive,
                Message = "This account is inactive."
            };
        }

        var profile = await _context.TouristProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.ProfileNotFound,
                Message = "Tourist profile was not found."
            };
        }

        return new TouristProfileResultDto
        {
            Success = true,
            Message = "Tourist profile retrieved successfully.",
            Data = ToResponse(user, profile)
        };
    }

    public async Task<TouristProfileResultDto> UpdateProfileAsync(
        int userId,
        UpdateTouristProfileRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.InvalidFullName,
                Message = "Full name is required."
            };
        }

        if (request.FullName.Trim().Length > 100)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.InvalidFullName,
                Message = "Full name cannot exceed 100 characters."
            };
        }

        if (request.PhoneNumber is not null &&
            request.PhoneNumber.Trim().Length > 20)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.InvalidPhoneNumber,
                Message = "Phone number cannot exceed 20 characters."
            };
        }

        if (request.Preferences is not null &&
            request.Preferences.Trim().Length > 1000)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.PreferencesTooLong,
                Message = "Preferences cannot exceed 1000 characters."
            };
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null || user.Role != UserRole.Tourist)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.Unauthorized,
                Message = "Only tourist accounts can update a tourist profile."
            };
        }

        if (!user.IsActive)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.AccountInactive,
                Message = "This account is inactive."
            };
        }

        var profile = await _context.TouristProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            return new TouristProfileResultDto
            {
                Success = false,
                Error = TouristProfileError.ProfileNotFound,
                Message = "Tourist profile was not found."
            };
        }

        user.FullName = request.FullName.Trim();

        if (request.PhoneNumber is not null)
        {
            user.PhoneNumber = request.PhoneNumber.Trim();
        }

        if (request.Preferences is not null)
        {
            profile.Preferences = request.Preferences.Trim();
        }

        await _context.SaveChangesAsync();

        return new TouristProfileResultDto
        {
            Success = true,
            Message = "Tourist profile updated successfully.",
            Data = ToResponse(user, profile)
        };
    }

    private static TouristProfileResponseDto ToResponse(
        User user,
        TouristProfile profile)
    {
        return new TouristProfileResponseDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Preferences = profile.Preferences,
            CreatedAt = user.CreatedAt
        };
    }
}
