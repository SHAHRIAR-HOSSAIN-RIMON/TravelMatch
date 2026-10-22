using TravelMatch.API.Models;

namespace TravelMatch.API.Tests;

internal sealed record DisputeScenario(
    int TouristUserId,
    int TouristProfileId,
    int UnrelatedTouristUserId,
    int GuideUserId,
    int AdminUserId,
    int TripRequestId,
    int DisputeId);

internal static class IntegrationTestData
{
    /// <summary>
    /// Seeds a TripRequest dispute where User.Id and TouristProfile.Id deliberately differ and
    /// an unrelated tourist's profile id collides with the id stored in TripRequest.TouristId.
    /// </summary>
    public static async Task<DisputeScenario> SeedTripRequestDisputeAsync(TestApplication app)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var tourist = new User
        {
            Id = 3,
            FullName = "Tourist User",
            Email = "tourist3@test.com",
            PhoneNumber = "01700000003",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var guide = new User
        {
            Id = 4,
            FullName = "Guide User",
            Email = "guide4@test.com",
            PhoneNumber = "01700000004",
            PasswordHash = "hash",
            Role = UserRole.Guide,
            IsActive = true
        };

        var unrelatedTourist = new User
        {
            Id = 99,
            FullName = "Unrelated Tourist",
            Email = "tourist99@test.com",
            PhoneNumber = "01700000099",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var admin = new User
        {
            Id = 900,
            FullName = "Admin User",
            Email = "admin900@test.com",
            PhoneNumber = "01700000900",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true
        };

        var touristProfile = new TouristProfile { Id = 33, UserId = tourist.Id };
        var collidingProfile = new TouristProfile { Id = tourist.Id, UserId = unrelatedTourist.Id };

        var tripRequest = new TripRequest
        {
            Id = 700,
            TouristId = tourist.Id,
            Destination = "Sundarbans",
            TripType = "Nature",
            StartDate = today.AddDays(10),
            EndDate = today.AddDays(14),
            NumberOfTravelers = 2,
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            Description = "Mangrove tour",
            TravelPreferences = "Nature",
            Status = TripRequestStatus.Completed,
            MatchedGuideId = guide.Id,
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        };

        var proposal = new Proposal
        {
            Id = 710,
            TripRequestId = tripRequest.Id,
            GuideId = guide.Id,
            ProposedPrice = 18000m,
            Availability = "Available",
            Status = ProposalStatus.Accepted
        };

        var dispute = new Dispute
        {
            Id = 950,
            ReportedByUserId = guide.Id,
            TripType = DisputeRelatedTripType.TripRequest,
            TripId = tripRequest.Id,
            Reason = "Payment disagreement",
            Description = "The final payment does not match the agreement.",
            Status = DisputeStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        await app.SeedAsync(db =>
        {
            db.Users.AddRange(tourist, guide, unrelatedTourist, admin);
            db.TouristProfiles.AddRange(touristProfile, collidingProfile);
            db.TripRequests.Add(tripRequest);
            db.Proposals.Add(proposal);
            db.Disputes.Add(dispute);
            return Task.CompletedTask;
        });

        return new DisputeScenario(
            tourist.Id,
            touristProfile.Id,
            unrelatedTourist.Id,
            guide.Id,
            admin.Id,
            tripRequest.Id,
            dispute.Id);
    }
}
