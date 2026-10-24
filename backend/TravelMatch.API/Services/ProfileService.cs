using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Profile;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class ProfileService : IProfileService
{
    private const int FullNameMaxLength = 100;
    private const int PhoneNumberMaxLength = 20;
    private const int TouristPreferencesMaxLength = 1000;
    private const int GuideBioMaxLength = 500;
    private const int GuideServiceAreaMaxLength = 100;
    private const int GuideSupportedTripTypesMaxLength = 500;
    private const int GuidePhotoUrlMaxLength = 500;
    private const int OrganizerBioMaxLength = 2000;
    private const int OrganizerNameMaxLength = 253;

    private readonly ApplicationDbContext _context;

    public ProfileService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProfileResultDto> GetMyProfileAsync(int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return Failure(ProfileError.UserNotFound, "User not found.");

        var data = ToDto(user);

        switch (user.Role)
        {
            case UserRole.Tourist:
            {
                var profile = await _context.TouristProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile is not null)
                    data.TouristProfile = ToDto(profile);
                break;
            }

            case UserRole.Guide:
            {
                var profile = await _context.GuideProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile is not null)
                    data.GuideProfile = ToDto(profile);
                break;
            }

            case UserRole.Organizer:
            {
                var profile = await _context.OrganizerProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile is not null)
                    data.OrganizerProfile = ToDto(profile);
                break;
            }
        }

        return new ProfileResultDto
        {
            Success = true,
            Message = "Profile retrieved successfully.",
            Data = data
        };
    }

    public async Task<ProfileResultDto> UpdateMyProfileAsync(
        int userId,
        UpdateProfileRequestDto request)
    {
        if (request is null)
            return Failure(ProfileError.InvalidInput, "A request body is required.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return Failure(ProfileError.UserNotFound, "User not found.");

        var validationMessage = ValidateRequest(request, user.Role);

        if (validationMessage is not null)
            return Failure(ProfileError.InvalidInput, validationMessage);

        TouristProfile? touristProfile = null;
        GuideProfile? guideProfile = null;
        OrganizerProfile? organizerProfile = null;

        // Load the caller's role-specific profile before changing anything so a missing
        // profile never results in a partially applied update.
        switch (user.Role)
        {
            case UserRole.Tourist:
                touristProfile = await _context.TouristProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (touristProfile is null)
                    return Failure(ProfileError.ProfileNotFound, "Tourist profile not found.");
                break;

            case UserRole.Guide:
                guideProfile = await _context.GuideProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (guideProfile is null)
                    return Failure(ProfileError.ProfileNotFound, "Guide profile not found.");
                break;

            case UserRole.Organizer:
                organizerProfile = await _context.OrganizerProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (organizerProfile is null)
                    return Failure(ProfileError.ProfileNotFound, "Organizer profile not found.");
                break;

            case UserRole.Admin:
                break;

            default:
                return Failure(ProfileError.InvalidInput, "Unsupported user role.");
        }

        if (request.FullName is not null)
            user.FullName = request.FullName.Trim();

        if (request.PhoneNumber is not null)
            user.PhoneNumber = request.PhoneNumber.Trim();

        switch (user.Role)
        {
            case UserRole.Tourist:
                if (request.Preferences is not null)
                    touristProfile!.Preferences = request.Preferences;
                break;

            case UserRole.Guide:
                if (request.Bio is not null)
                    guideProfile!.Bio = request.Bio;

                if (request.ServiceArea is not null)
                    guideProfile!.ServiceArea = request.ServiceArea;

                if (request.ExperienceYears.HasValue)
                    guideProfile!.ExperienceYears = request.ExperienceYears.Value;

                if (request.SupportedTripTypes is not null)
                    guideProfile!.SupportedTripTypes = request.SupportedTripTypes;

                if (request.PhotoUrl is not null)
                    guideProfile!.PhotoUrl = request.PhotoUrl;
                break;

            case UserRole.Organizer:
                if (request.GroupOrganizationName is not null)
                    organizerProfile!.GroupOrganizationName = request.GroupOrganizationName;

                if (request.Bio is not null)
                    organizerProfile!.Bio = request.Bio;
                break;
        }

        await _context.SaveChangesAsync();

        var data = ToDto(user);

        if (touristProfile is not null)
            data.TouristProfile = ToDto(touristProfile);

        if (guideProfile is not null)
            data.GuideProfile = ToDto(guideProfile);

        if (organizerProfile is not null)
            data.OrganizerProfile = ToDto(organizerProfile);

        return new ProfileResultDto
        {
            Success = true,
            Message = "Profile updated successfully.",
            Data = data
        };
    }

    private static string? ValidateRequest(UpdateProfileRequestDto request, UserRole? userRole)
    {
        var hasAnyField = request.FullName is not null
            || request.PhoneNumber is not null
            || request.Preferences is not null
            || request.Bio is not null
            || request.ServiceArea is not null
            || request.ExperienceYears.HasValue
            || request.SupportedTripTypes is not null
            || request.PhotoUrl is not null
            || request.GroupOrganizationName is not null;

        if (!hasAnyField)
            return "At least one editable profile field must be provided.";

        if (userRole is not null)
        {
            var roleError = ValidateRoleFields(request, userRole.Value);

            if (roleError is not null)
                return roleError;
        }

        if (request.FullName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return "FullName cannot be empty.";

            if (request.FullName.Trim().Length > FullNameMaxLength)
                return $"FullName cannot exceed {FullNameMaxLength} characters.";
        }

        if (request.PhoneNumber is not null)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return "PhoneNumber cannot be empty.";

            if (request.PhoneNumber.Trim().Length > PhoneNumberMaxLength)
                return $"PhoneNumber cannot exceed {PhoneNumberMaxLength} characters.";
        }

        if (request.ExperienceYears is not null && request.ExperienceYears.Value < 0)
            return "ExperienceYears cannot be negative.";

        return userRole switch
        {
            UserRole.Tourist when (request.Preferences?.Length ?? 0) > TouristPreferencesMaxLength =>
                $"Preferences cannot exceed {TouristPreferencesMaxLength} characters.",

            UserRole.Guide when (request.Bio?.Length ?? 0) > GuideBioMaxLength =>
                $"Bio cannot exceed {GuideBioMaxLength} characters for a guide.",

            UserRole.Guide when (request.ServiceArea?.Length ?? 0) > GuideServiceAreaMaxLength =>
                $"ServiceArea cannot exceed {GuideServiceAreaMaxLength} characters.",

            UserRole.Guide when (request.SupportedTripTypes?.Length ?? 0) > GuideSupportedTripTypesMaxLength =>
                $"SupportedTripTypes cannot exceed {GuideSupportedTripTypesMaxLength} characters.",

            UserRole.Guide when (request.PhotoUrl?.Length ?? 0) > GuidePhotoUrlMaxLength =>
                $"PhotoUrl cannot exceed {GuidePhotoUrlMaxLength} characters.",

            UserRole.Organizer when (request.Bio?.Length ?? 0) > OrganizerBioMaxLength =>
                $"Bio cannot exceed {OrganizerBioMaxLength} characters for an organizer.",

            UserRole.Organizer when (request.GroupOrganizationName?.Length ?? 0) > OrganizerNameMaxLength =>
                $"GroupOrganizationName cannot exceed {OrganizerNameMaxLength} characters.",

            _ => null
        };
    }

    private static string? ValidateRoleFields(UpdateProfileRequestDto request, UserRole userRole)
    {
        switch (userRole)
        {
            case UserRole.Tourist:
                if (request.Bio is not null
                    || request.ServiceArea is not null
                    || request.ExperienceYears.HasValue
                    || request.SupportedTripTypes is not null
                    || request.PhotoUrl is not null
                    || request.GroupOrganizationName is not null)
                    return "Bio, ServiceArea, ExperienceYears, SupportedTripTypes, PhotoUrl and GroupOrganizationName are not editable for the Tourist role.";
                break;

            case UserRole.Guide:
                if (request.Preferences is not null || request.GroupOrganizationName is not null)
                    return "Preferences and GroupOrganizationName are not editable for the Guide role.";
                break;

            case UserRole.Organizer:
                if (request.Preferences is not null
                    || request.ServiceArea is not null
                    || request.ExperienceYears.HasValue
                    || request.SupportedTripTypes is not null
                    || request.PhotoUrl is not null)
                    return "Preferences, ServiceArea, ExperienceYears, SupportedTripTypes and PhotoUrl are not editable for the Organizer role.";
                break;

            case UserRole.Admin:
                if (request.Preferences is not null
                    || request.Bio is not null
                    || request.ServiceArea is not null
                    || request.ExperienceYears.HasValue
                    || request.SupportedTripTypes is not null
                    || request.PhotoUrl is not null
                    || request.GroupOrganizationName is not null)
                    return "Admin accounts have no role-specific profile fields to update.";
                break;

            default:
                return "Unsupported user role.";
        }

        return null;
    }

    private static ProfileResultDto Failure(ProfileError error, string message) => new()
    {
        Success = false,
        Error = error,
        Message = message
    };

    private static ProfileResponseDto ToDto(User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        Role = user.Role.ToString(),
        CreatedAt = user.CreatedAt,
        IsActive = user.IsActive
    };

    private static TouristProfileDto ToDto(TouristProfile profile) => new()
    {
        Id = profile.Id,
        Preferences = profile.Preferences
    };

    private static GuideProfileDto ToDto(GuideProfile profile) => new()
    {
        Id = profile.Id,
        Bio = profile.Bio,
        ServiceArea = profile.ServiceArea,
        SupportedTripTypes = profile.SupportedTripTypes,
        ExperienceYears = profile.ExperienceYears,
        AverageRating = profile.AverageRating,
        VerificationStatus = profile.VerificationStatus.ToString(),
        PhotoUrl = profile.PhotoUrl
    };

    private static OrganizerProfileDto ToDto(OrganizerProfile profile) => new()
    {
        Id = profile.Id,
        GroupOrganizationName = profile.GroupOrganizationName,
        Bio = profile.Bio
    };
}
