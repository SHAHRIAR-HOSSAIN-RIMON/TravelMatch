using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Data;

public static class DbSeeder
{
    public const string GuideEmail = "guide@travelmatch.test";
    public const string TouristEmail = "tourist@travelmatch.test";
    public const string DefaultPassword = "Password123";

    public static async Task SeedAsync(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher)
    {
        await context.Database.MigrateAsync();

        if (await context.Users.AnyAsync())
        {
            return;
        }

        var guideUser = new User
        {
            FullName = "Rafiul Islam",
            Email = GuideEmail,
            PhoneNumber = "01700000001",
            PasswordHash = passwordHasher.HashPassword(DefaultPassword),
            Role = UserRole.Guide
        };

        var secondGuideUser = new User
        {
            FullName = "Nusrat Jahan",
            Email = "guide2@travelmatch.test",
            PhoneNumber = "01700000002",
            PasswordHash = passwordHasher.HashPassword(DefaultPassword),
            Role = UserRole.Guide
        };

        var touristUser = new User
        {
            FullName = "Ayesha Siddiqua",
            Email = TouristEmail,
            PhoneNumber = "01800000001",
            PasswordHash = passwordHasher.HashPassword(DefaultPassword),
            Role = UserRole.Tourist
        };

        context.Users.AddRange(guideUser, secondGuideUser, touristUser);
        await context.SaveChangesAsync();

        context.GuideProfiles.AddRange(
            new GuideProfile
            {
                UserId = guideUser.Id,
                Bio = "Ten years guiding tours across Dhaka, Chattogram and the hill tracts.",
                ServiceArea = "Dhaka, Chattogram",
                ExperienceYears = 10,
                AverageRating = 4.8m,
                VerificationStatus = VerificationStatus.Verified
            },
            new GuideProfile
            {
                UserId = secondGuideUser.Id,
                Bio = "Food, culture and heritage walks in Old Dhaka.",
                ServiceArea = "Old Dhaka",
                ExperienceYears = 6,
                AverageRating = 4.5m,
                VerificationStatus = VerificationStatus.Verified
            });

        context.TouristProfiles.Add(new TouristProfile
        {
            UserId = touristUser.Id,
            Preferences = "Photography, local food, small groups"
        });

        var today = DateOnly.FromDateTime(
            DateTime.Now.AddDays(21));

        context.TripRequests.AddRange(
            new TripRequest
            {
                TouristId = touristUser.Id,
                Destination = "Sundarbans",
                StartDate = today,
                EndDate = today.AddDays(3),
                NumberOfTravelers = 2,
                Budget = 45000m,
                Description = "Looking for a boat and guide for a wildlife trip. Prefer sunrise walks and local village stays.",
                Status = TripRequestStatus.Open,
                CreatedAt = DateTime.UtcNow
            },
            new TripRequest
            {
                TouristId = touristUser.Id,
                Destination = "Srimangal",
                StartDate = today.AddDays(10),
                EndDate = today.AddDays(13),
                NumberOfTravelers = 4,
                Budget = 80000m,
                Description = "Tea gardens, tea estate stay and a night walk in Ratargul. Four adults.",
                Status = TripRequestStatus.Open,
                CreatedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync();
    }
}
