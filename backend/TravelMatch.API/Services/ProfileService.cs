using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Profiles;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class ProfileService : IProfileService
{
    private const string LoadFailedMessage =
        "Couldn't load the profile.";

    private const string UpdateFailedMessage =
        "Couldn't update the profile.";

    private const string WrongSectionMessage =
        "The profile section in the request does not match your account role.";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        ApplicationDbContext context,
        ILogger<ProfileService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProfileResultDto> GetMyProfileAsync(int userId)
    {
        try
        {
            var profile = await LoadProfileAsync(userId);

            if (profile is null)
            {
                return UserNotFound();
            }

            return new ProfileResultDto
            {
                Success = true,
                Error = ProfileError.None,
                Message = "Profile loaded successfully.",
                Data = ToDto(profile)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the profile of user {UserId}.", userId);

            return new ProfileResultDto
            {
                Success = false,
                Error = ProfileError.ServerError,
                Message = LoadFailedMessage
            };
        }
    }

    public async Task<ProfileResultDto> UpdateMyProfileAsync(
        int userId,
        UpdateProfileDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return new ProfileResultDto
            {
                Success = false,
                Error = ProfileError.ProfileSectionMismatch,
                Message = "Full name and phone number are required."
            };
        }

        try
        {
            var profile = await LoadProfileAsync(userId, tracked: true);

            if (profile is null)
            {
                return UserNotFound();
            }

            if (!MatchesRole(request, profile.Role))
            {
                return new ProfileResultDto
                {
                    Success = false,
                    Error = ProfileError.ProfileSectionMismatch,
                    Message = WrongSectionMessage
                };
            }

            if (!HasRoleProfile(profile))
            {
                return new ProfileResultDto
                {
                    Success = false,
                    Error = ProfileError.ProfileNotFound,
                    Message = "Your account profile is incomplete. Contact support."
                };
            }

            profile.User.FullName = request.FullName.Trim();
            profile.User.PhoneNumber = request.PhoneNumber.Trim();

            switch (profile.Role)
            {
                case UserRole.Guide when request.Guide is not null:
                    ApplyGuideUpdates(profile.GuideProfile, request.Guide);
                    break;

                case UserRole.Tourist when request.Tourist is not null:
                    ApplyTouristUpdates(profile.TouristProfile, request.Tourist);
                    break;

                case UserRole.Organizer when request.Organizer is not null:
                    ApplyOrganizerUpdates(profile.OrganizerProfile, request.Organizer);
                    break;
            }

            await _context.SaveChangesAsync();

            return new ProfileResultDto
            {
                Success = true,
                Error = ProfileError.None,
                Message = "Profile updated successfully.",
                Data = ToDto(profile)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update the profile of user {UserId}.", userId);

            return new ProfileResultDto
            {
                Success = false,
                Error = ProfileError.ServerError,
                Message = UpdateFailedMessage
            };
        }
    }

    private async Task<ProfileRecord?> LoadProfileAsync(
        int userId,
        bool tracked = false)
    {
        var users = tracked
            ? _context.Users
            : _context.Users.AsNoTracking();

        var user = await users
            .Where(u => u.Id == userId && u.IsActive)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return null;
        }

        var record = new ProfileRecord
        {
            User = user,
            Role = user.Role
        };

        switch (user.Role)
        {
            case UserRole.Guide:
                record.GuideProfile = await (
                    tracked
                        ? _context.GuideProfiles
                        : _context.GuideProfiles.AsNoTracking())
                    .FirstOrDefaultAsync(p => p.UserId == userId);
                break;

            case UserRole.Tourist:
                record.TouristProfile = await (
                    tracked
                        ? _context.TouristProfiles
                        : _context.TouristProfiles.AsNoTracking())
                    .FirstOrDefaultAsync(p => p.UserId == userId);
                break;

            case UserRole.Organizer:
                record.OrganizerProfile = await (
                    tracked
                        ? _context.OrganizerProfiles
                        : _context.OrganizerProfiles.AsNoTracking())
                    .FirstOrDefaultAsync(p => p.UserId == userId);
                break;
        }

        return record;
    }

    private static bool HasRoleProfile(ProfileRecord record)
    {
        return record.Role switch
        {
            UserRole.Guide => record.GuideProfile is not null,
            UserRole.Tourist => record.TouristProfile is not null,
            UserRole.Organizer => record.OrganizerProfile is not null,
            _ => true
        };
    }

    private static bool MatchesRole(UpdateProfileDto request, UserRole role)
    {
        var hasGuide = request.Guide is not null;
        var hasTourist = request.Tourist is not null;
        var hasOrganizer = request.Organizer is not null;

        if (!hasGuide && !hasTourist && !hasOrganizer)
        {
            return true;
        }

        return role switch
        {
            UserRole.Guide => hasGuide && !hasTourist && !hasOrganizer,
            UserRole.Tourist => hasTourist && !hasGuide && !hasOrganizer,
            UserRole.Organizer => hasOrganizer && !hasGuide && !hasTourist,
            _ => false
        };
    }

    private static void ApplyGuideUpdates(
        GuideProfile? profile,
        GuideProfileUpdateDto update)
    {
        if (profile is null)
        {
            return;
        }

        if (update.Bio is not null)
        {
            profile.Bio = update.Bio.Trim();
        }

        if (update.ServiceArea is not null)
        {
            profile.ServiceArea = update.ServiceArea.Trim();
        }

        if (update.ExperienceYears.HasValue)
        {
            profile.ExperienceYears = update.ExperienceYears.Value;
        }
    }

    private static void ApplyTouristUpdates(
        TouristProfile? profile,
        TouristProfileUpdateDto update)
    {
        if (profile is null || update.Preferences is null)
        {
            return;
        }

        profile.Preferences = update.Preferences.Trim();
    }

    private static void ApplyOrganizerUpdates(
        OrganizerProfile? profile,
        OrganizerProfileUpdateDto update)
    {
        if (profile is null)
        {
            return;
        }

        if (update.GroupOrganizationName is not null)
        {
            profile.GroupOrganizationName = update.GroupOrganizationName.Trim();
        }

        if (update.Bio is not null)
        {
            profile.Bio = update.Bio.Trim();
        }
    }

    private static ProfileResultDto UserNotFound()
    {
        return new ProfileResultDto
        {
            Success = false,
            Error = ProfileError.UserNotFound,
            Message = "User profile not found."
        };
    }

    private static ProfileResponseDto ToDto(ProfileRecord record)
    {
        return new ProfileResponseDto
        {
            Id = record.User.Id,
            FullName = record.User.FullName,
            Email = record.User.Email,
            PhoneNumber = record.User.PhoneNumber,
            Role = record.Role.ToString(),
            CreatedAt = record.User.CreatedAt,
            GuideProfile = record.GuideProfile is null
                ? null
                : new GuideProfileDto
                {
                    Id = record.GuideProfile.Id,
                    Bio = record.GuideProfile.Bio,
                    ServiceArea = record.GuideProfile.ServiceArea,
                    ExperienceYears = record.GuideProfile.ExperienceYears,
                    AverageRating = record.GuideProfile.AverageRating,
                    VerificationStatus =
                        record.GuideProfile.VerificationStatus.ToString()
                },
            TouristProfile = record.TouristProfile is null
                ? null
                : new TouristProfileDto
                {
                    Id = record.TouristProfile.Id,
                    Preferences = record.TouristProfile.Preferences
                },
            OrganizerProfile = record.OrganizerProfile is null
                ? null
                : new OrganizerProfileDto
                {
                    Id = record.OrganizerProfile.Id,
                    GroupOrganizationName = record.OrganizerProfile.GroupOrganizationName,
                    Bio = record.OrganizerProfile.Bio
                }
        };
    }

    private sealed class ProfileRecord
    {
        public User User { get; set; } = new();

        public UserRole Role { get; set; }

        public GuideProfile? GuideProfile { get; set; }

        public TouristProfile? TouristProfile { get; set; }

        public OrganizerProfile? OrganizerProfile { get; set; }
    }
}
