using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TravelMatch.API.DTOs.Auth;
using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.DTOs.Registrations;
using TravelMatch.API.DTOs.Reviews;
using TravelMatch.API.Models;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// End-to-end tests through the real ASP.NET Core pipeline: routing, JWT authentication,
/// role authorization, controllers, services and EF Core (isolated InMemory database).
/// </summary>
public class HttpPipelineIntegrationTests
{
    private sealed record OrganizedTripScenario(
        int OrganizerUserId,
        int TouristUserId,
        int TouristProfileId,
        int TripId);

    private sealed record ReviewScenario(
        int TouristUserId,
        int GuideUserId,
        int GuideProfileId,
        int TripRequestId);

    private static async Task<OrganizedTripScenario> SeedOrganizedTripScenarioAsync(TestApplication app)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var organizer = new User
        {
            Id = 20,
            FullName = "Organizer User",
            Email = "organizer20@test.com",
            PhoneNumber = "01700000020",
            PasswordHash = "hash",
            Role = UserRole.Organizer,
            IsActive = true
        };

        var tourist = new User
        {
            Id = 21,
            FullName = "Tourist User",
            Email = "tourist21@test.com",
            PhoneNumber = "01700000021",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var organizerProfile = new OrganizerProfile
        {
            Id = 60,
            UserId = organizer.Id,
            GroupOrganizationName = "Test Group",
            Bio = "Group organizer"
        };

        var touristProfile = new TouristProfile
        {
            Id = 61,
            UserId = tourist.Id,
            Preferences = "Beach"
        };

        var trip = new OrganizedTrip
        {
            Id = 800,
            OrganizerId = organizerProfile.Id,
            Title = "Cox's Bazar Weekend",
            Destination = "Cox's Bazar",
            Description = "Beach weekend",
            StartDate = today.AddDays(10),
            EndDate = today.AddDays(12),
            MaxParticipants = 5,
            PricePerPerson = 250m,
            Inclusions = "Hotel and transport",
            RegistrationDeadline = today.AddDays(5),
            Status = OrganizedTripStatus.RegistrationOpen,
            CreatedAt = DateTime.UtcNow
        };

        await app.SeedAsync(db =>
        {
            db.Users.AddRange(organizer, tourist);
            db.OrganizerProfiles.Add(organizerProfile);
            db.TouristProfiles.Add(touristProfile);
            db.OrganizedTrips.Add(trip);
            return Task.CompletedTask;
        });

        return new OrganizedTripScenario(organizer.Id, tourist.Id, touristProfile.Id, trip.Id);
    }

    private static async Task<ReviewScenario> SeedCompletedTripAsync(TestApplication app)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var tourist = new User
        {
            Id = 10,
            FullName = "Tourist User",
            Email = "tourist10@test.com",
            PhoneNumber = "01700000010",
            PasswordHash = "hash",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var guide = new User
        {
            Id = 11,
            FullName = "Guide User",
            Email = "guide11@test.com",
            PhoneNumber = "01700000011",
            PasswordHash = "hash",
            Role = UserRole.Guide,
            IsActive = true
        };

        // Review.GuideId references GuideProfile.Id, deliberately different from the guide's User.Id.
        var guideProfile = new GuideProfile
        {
            Id = 500,
            UserId = guide.Id,
            Bio = "Mountain guide",
            ServiceArea = "Bandarban",
            SupportedTripTypes = "Trekking",
            ExperienceYears = 5,
            VerificationStatus = VerificationStatus.Verified
        };

        var tripRequest = new TripRequest
        {
            Id = 720,
            TouristId = tourist.Id,
            Destination = "Bandarban",
            TripType = "Trekking",
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(-5),
            NumberOfTravelers = 2,
            Budget = 20000m,
            BudgetMin = 15000m,
            BudgetMax = 25000m,
            Description = "Hill trek",
            TravelPreferences = "Trekking",
            Status = TripRequestStatus.Completed,
            MatchedGuideId = guide.Id,
            CompletedAt = DateTime.UtcNow.AddDays(-5)
        };

        await app.SeedAsync(db =>
        {
            db.Users.AddRange(tourist, guide);
            db.GuideProfiles.Add(guideProfile);
            db.TripRequests.Add(tripRequest);
            return Task.CompletedTask;
        });

        return new ReviewScenario(tourist.Id, guide.Id, guideProfile.Id, tripRequest.Id);
    }

    [Fact]
    public async Task HttpPipeline_EnforcesAuthenticationAndRoleAuthorization()
    {
        await using var app = await TestApplication.StartAsync();
        var seed = await SeedOrganizedTripScenarioAsync(app);

        var anonymousMe = await app.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMe.StatusCode);

        var anonymousRegistrations = await app.Client.GetAsync(
            $"/api/organized-trips/{seed.TripId}/registrations/my");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousRegistrations.StatusCode);

        app.UseToken(await app.CreateTokenAsync(seed.TouristUserId));

        var me = await app.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(profile);
        Assert.Equal(seed.TouristUserId, profile!.Id);
        Assert.Equal("Tourist", profile.Role);

        var myPayments = await app.Client.GetAsync("/api/payments/my");
        Assert.Equal(HttpStatusCode.OK, myPayments.StatusCode);

        app.UseToken(await app.CreateTokenAsync(seed.OrganizerUserId));

        var paymentsAsOrganizer = await app.Client.GetAsync("/api/payments/my");
        Assert.Equal(HttpStatusCode.Forbidden, paymentsAsOrganizer.StatusCode);

        var registrationAsOrganizer = await app.Client.PostAsJsonAsync(
            $"/api/organized-trips/{seed.TripId}/registrations",
            new CreateRegistrationDto { NumberOfTravelers = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, registrationAsOrganizer.StatusCode);
    }

    [Fact]
    public async Task RegistrationAndPaymentPipeline_ChargesFullRegistrationTotal()
    {
        await using var app = await TestApplication.StartAsync();
        var seed = await SeedOrganizedTripScenarioAsync(app);

        app.UseToken(await app.CreateTokenAsync(seed.TouristUserId));

        var createResponse = await app.Client.PostAsJsonAsync(
            $"/api/organized-trips/{seed.TripId}/registrations",
            new CreateRegistrationDto { NumberOfTravelers = 3 });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<RegistrationResultDto>();
        Assert.NotNull(created);
        Assert.True(created!.Success);
        Assert.NotNull(created.Data);
        Assert.Equal(3, created.Data!.NumberOfTravelers);
        Assert.Equal(750m, created.Data.TotalPrice);
        Assert.Equal("PendingPayment", created.Data.Status);

        var registrationId = created.Data.Id;

        var payResponse = await app.Client.PostAsJsonAsync(
            $"/api/payments/registration/{registrationId}",
            new PayRegistrationRequestDto());

        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);
        var paid = await payResponse.Content.ReadFromJsonAsync<PaymentResultDto>();
        Assert.NotNull(paid);
        Assert.True(paid!.Success);
        Assert.NotNull(paid.Data);
        Assert.Equal(750m, paid.Data!.Amount);
        Assert.Equal(PaymentStatus.Held, paid.Data.Status);
        Assert.Equal(seed.TouristUserId, paid.Data.TouristUserId);
        Assert.Equal(registrationId, paid.Data.RegistrationId);

        var myRegistrationsResponse = await app.Client.GetAsync(
            $"/api/organized-trips/{seed.TripId}/registrations/my");
        Assert.Equal(HttpStatusCode.OK, myRegistrationsResponse.StatusCode);
        var myRegistrations = await myRegistrationsResponse.Content
            .ReadFromJsonAsync<RegistrationListResultDto>();
        var registration = Assert.Single(myRegistrations!.Data!);
        Assert.Equal("Confirmed", registration.Status);
        Assert.Equal(750m, registration.TotalPrice);

        var repeatResponse = await app.Client.PostAsJsonAsync(
            $"/api/payments/registration/{registrationId}",
            new PayRegistrationRequestDto());
        Assert.Equal(HttpStatusCode.Conflict, repeatResponse.StatusCode);
        var repeat = await repeatResponse.Content.ReadFromJsonAsync<PaymentResultDto>();
        Assert.NotNull(repeat);
        Assert.False(repeat!.Success);
        Assert.Equal(PaymentError.AlreadyPaid, repeat.Error);

        var storedPayments = await app.WithDbAsync(db => db.Payments
            .Where(p => p.RegistrationId == registrationId)
            .ToListAsync());
        var storedPayment = Assert.Single(storedPayments);
        Assert.Equal(750m, storedPayment.Amount);
        Assert.Equal(PaymentStatus.Held, storedPayment.Status);
    }

    [Fact]
    public async Task RegistrationPaymentPipeline_RejectsInconsistentStoredTotal()
    {
        await using var app = await TestApplication.StartAsync();
        var seed = await SeedOrganizedTripScenarioAsync(app);

        await app.SeedAsync(db =>
        {
            db.Registrations.Add(new Registration
            {
                Id = 851,
                OrganizedTripId = seed.TripId,
                TouristId = seed.TouristProfileId,
                NumberOfTravelers = 2,
                TotalPrice = 250m,
                Status = RegistrationStatus.PendingPayment,
                RegisteredAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        app.UseToken(await app.CreateTokenAsync(seed.TouristUserId));

        var response = await app.Client.PostAsJsonAsync(
            "/api/payments/registration/851",
            new PayRegistrationRequestDto());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaymentResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal(PaymentError.InvalidRequest, result.Error);
        Assert.Contains("500.00", result.Message);

        var paymentCount = await app.WithDbAsync(db =>
            db.Payments.CountAsync(p => p.RegistrationId == 851));
        Assert.Equal(0, paymentCount);

        var registrationStatus = await app.WithDbAsync(db => db.Registrations
            .Where(r => r.Id == 851)
            .Select(r => r.Status)
            .SingleAsync());
        Assert.Equal(RegistrationStatus.PendingPayment, registrationStatus);
    }

    [Fact]
    public async Task ReviewPipeline_SubmitsAndRetrievesReview()
    {
        await using var app = await TestApplication.StartAsync();
        var seed = await SeedCompletedTripAsync(app);

        var anonymous = await app.Client.GetAsync($"/api/reviews/guide/{seed.GuideProfileId}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        app.UseToken(await app.CreateTokenAsync(seed.TouristUserId));

        var submitResponse = await app.Client.PostAsJsonAsync("/api/reviews", new CreateReviewRequestDto
        {
            GuideId = seed.GuideProfileId,
            TripRequestId = seed.TripRequestId,
            Rating = 5,
            Comment = "Outstanding guiding"
        });

        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<ReviewResultDto>();
        Assert.NotNull(submitted);
        Assert.True(submitted!.Success);
        Assert.NotNull(submitted.Data);
        Assert.Equal(seed.GuideProfileId, submitted.Data!.GuideId);
        Assert.Equal(seed.TouristUserId, submitted.Data.TouristId);
        Assert.Equal("Tourist User", submitted.Data.TouristName);
        Assert.Equal("Guide User", submitted.Data.GuideName);

        var listResponse = await app.Client.GetAsync($"/api/reviews/guide/{seed.GuideProfileId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ReviewListResultDto>();
        Assert.NotNull(list);
        Assert.True(list!.Success);
        Assert.Equal(1, list.TotalCount);
        var review = Assert.Single(list.Data!);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Outstanding guiding", review.Comment);

        var ratingResponse = await app.Client.GetAsync(
            $"/api/reviews/guide/{seed.GuideProfileId}/rating");
        Assert.Equal(HttpStatusCode.OK, ratingResponse.StatusCode);
        var rating = await ratingResponse.Content.ReadFromJsonAsync<GuideRatingDto>();
        Assert.NotNull(rating);
        Assert.Equal(5m, rating!.AverageRating);
        Assert.Equal(1, rating.ReviewCount);

        app.UseToken(await app.CreateTokenAsync(seed.GuideUserId));
        var asGuide = await app.Client.PostAsJsonAsync("/api/reviews", new CreateReviewRequestDto
        {
            GuideId = seed.GuideProfileId,
            TripRequestId = seed.TripRequestId,
            Rating = 5
        });
        Assert.Equal(HttpStatusCode.Forbidden, asGuide.StatusCode);

        var storedAverage = await app.WithDbAsync(db => db.GuideProfiles
            .Where(g => g.Id == seed.GuideProfileId)
            .Select(g => g.AverageRating)
            .SingleAsync());
        Assert.Equal(5m, storedAverage);
    }

    [Fact]
    public async Task DisputeResolutionPipeline_NotifiesCorrectTouristUser()
    {
        await using var app = await TestApplication.StartAsync();
        var seed = await IntegrationTestData.SeedTripRequestDisputeAsync(app);

        app.UseToken(await app.CreateTokenAsync(seed.TouristUserId));
        var asTourist = await app.Client.PostAsJsonAsync(
            $"/api/disputes/{seed.DisputeId}/resolve",
            new ResolveDisputeDto { ResolutionNote = "Refund agreed" });
        Assert.Equal(HttpStatusCode.Forbidden, asTourist.StatusCode);

        app.UseToken(await app.CreateTokenAsync(seed.AdminUserId));
        var resolveResponse = await app.Client.PostAsJsonAsync(
            $"/api/disputes/{seed.DisputeId}/resolve",
            new ResolveDisputeDto { ResolutionNote = "Refund agreed" });

        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<DisputeResultDto>();
        Assert.NotNull(resolved);
        Assert.True(resolved!.Success);
        Assert.Equal("Resolved", resolved.Data!.Status);
        Assert.Equal(seed.AdminUserId, resolved.Data.ResolvedByUserId);
        Assert.Equal("Refund agreed", resolved.Data.ResolutionNote);

        app.UseToken(await app.CreateTokenAsync(seed.TouristUserId));
        var notificationsResponse = await app.Client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, notificationsResponse.StatusCode);
        var notifications = await notificationsResponse.Content
            .ReadFromJsonAsync<List<NotificationDto>>();
        Assert.NotNull(notifications);
        var touristNotification = Assert.Single(
            notifications!, n => n.Title == "Dispute resolved");
        Assert.Equal(seed.TouristUserId, touristNotification.RecipientUserId);
        Assert.Contains("Refund agreed", touristNotification.Message);

        var recipientIds = await app.WithDbAsync(db => db.UserNotifications
            .Where(n => n.Title == "Dispute resolved")
            .Select(n => n.RecipientUserId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync());

        Assert.Equal(
            new[] { seed.TouristUserId, seed.GuideUserId }.OrderBy(id => id),
            recipientIds);
        Assert.DoesNotContain(seed.UnrelatedTouristUserId, recipientIds);

        var persistedDispute = await app.WithDbAsync(db =>
            db.Disputes.SingleAsync(d => d.Id == seed.DisputeId));
        Assert.Equal(DisputeStatus.Resolved, persistedDispute.Status);
        Assert.Equal("Refund agreed", persistedDispute.ResolutionNote);
        Assert.Equal(seed.AdminUserId, persistedDispute.ResolvedByUserId);
    }
}
