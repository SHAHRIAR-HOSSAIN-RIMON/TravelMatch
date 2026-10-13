
using TravelMatch.API.Data;
using TravelMatch.API.Models;

namespace TravelMatch.Tests.TestSupport;

public static class Seed
{
    public static User AddTourist(
        ApplicationDbContext context,
        string fullName = "Tourist One",
        string email = "tourist1@example.com",
        bool withProfile = true)
    {
        var user = new User
        {
            FullName = fullName,
            Email = email,
            PhoneNumber = "+8801000000001",
            PasswordHash = "hash",
            Role = UserRole.Tourist
        };

        context.Users.Add(user);
        context.SaveChanges();

        if (withProfile)
        {
            context.TouristProfiles.Add(new TouristProfile
            {
                UserId = user.Id,
                Preferences = "Beaches and history"
            });
            context.SaveChanges();
        }

        return user;
    }

    public static User AddGuide(
        ApplicationDbContext context,
        string fullName = "Guide One",
        string email = "guide1@example.com")
    {
        var user = new User
        {
            FullName = fullName,
            Email = email,
            PhoneNumber = "+8801000000002",
            PasswordHash = "hash",
            Role = UserRole.Guide
        };

        context.Users.Add(user);
        context.SaveChanges();

        return user;
    }

    public static TripRequest AddTripRequest(
        ApplicationDbContext context,
        int touristId,
        TripRequestStatus status = TripRequestStatus.Pending,
        string destination = "Cox's Bazar")
    {
        var tripRequest = new TripRequest
        {
            TouristId = touristId,
            Destination = destination,
            StartDate = new DateOnly(2027, 1, 10),
            EndDate = new DateOnly(2027, 1, 15),
            NumberOfTravelers = 2,
            Budget = 50000m,
            Description = "Beach trip",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        context.TripRequests.Add(tripRequest);
        context.SaveChanges();

        return tripRequest;
    }
}
