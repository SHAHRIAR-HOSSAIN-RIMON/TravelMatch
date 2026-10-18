using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.DTOs.Proposals;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.DTOs.Itineraries;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for NotificationService: list, mark as read, and authorization.
/// Uses EF Core InMemory provider so no real database is needed.
/// </summary>
public class NotificationServiceTests
{
    private static ApplicationDbContext BuildContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static User MakeUser(int id, string email = "") => new()
    {
        Id = id,
        FullName = $"Test User {id}",
        Email = string.IsNullOrEmpty(email) ? $"user{id}@test.com" : email,
        PhoneNumber = $"017000000{id:D2}",
        PasswordHash = "hash",
        Role = UserRole.Tourist,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static User MakeGuide(int id) => new()
    {
        Id = id,
        FullName = $"Guide {id}",
        Email = $"guide{id}@test.com",
        PhoneNumber = $"017000000{id:D2}",
        PasswordHash = "hash",
        Role = UserRole.Guide,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static User MakeOrganizer(int id) => new()
    {
        Id = id,
        FullName = $"Organizer {id}",
        Email = $"organizer{id}@test.com",
        PhoneNumber = $"017000000{id:D2}",
        PasswordHash = "hash",
        Role = UserRole.Organizer,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static TouristProfile MakeTouristProfile(int userId) => new()
    {
        Id = userId,
        UserId = userId,
        Preferences = ""
    };

    private static GuideProfile MakeGuideProfile(int userId) => new()
    {
        UserId = userId,
        Bio = "Test guide",
        ServiceArea = "Dhaka",
        SupportedTripTypes = "Adventure",
        ExperienceYears = 5,
        AverageRating = 4.0m,
        VerificationStatus = VerificationStatus.Verified
    };

    private static OrganizerProfile MakeOrganizerProfile(int userId, int profileId) => new()
    {
        Id = profileId,
        UserId = userId,
        GroupOrganizationName = "Test Org",
        Bio = "Test bio"
    };

    private static TripRequest MakeTripRequest(int id, int touristId,
        TripRequestStatus status = TripRequestStatus.Pending) => new()
    {
        Id = id,
        TouristId = touristId,
        Destination = "Cox's Bazar",
        TripType = "Beach",
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35)),
        NumberOfTravelers = 2,
        Budget = 20000m,
        BudgetMin = 15000m,
        BudgetMax = 25000m,
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    private static OrganizedTrip MakeOrganizedTrip(int id, int organizerId) => new()
    {
        Id = id,
        OrganizerId = organizerId,
        Title = "Test Trip",
        Destination = "Sylhet",
        Description = "Test description",
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(65)),
        MaxParticipants = 10,
        PricePerPerson = 15000m,
        Inclusions = "Hotel, meals",
        RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        Status = OrganizedTripStatus.RegistrationOpen,
        CreatedAt = DateTime.UtcNow
    };

    private static NotificationService BuildService(ApplicationDbContext ctx)
    {
        var logger = new LoggerFactory().CreateLogger<NotificationService>();
        return new NotificationService(ctx, logger, new NoopNotificationSender());
    }

    // ─── GetMyNotificationsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetMyNotificationsAsync_ReturnsOnlyOwnNotifications()
    {
        await using var ctx = BuildContext(nameof(GetMyNotificationsAsync_ReturnsOnlyOwnNotifications));
        ctx.Users.Add(MakeUser(1));
        ctx.Users.Add(MakeUser(2));
        await ctx.SaveChangesAsync();

        ctx.UserNotifications.AddRange(
            new UserNotification { RecipientUserId = 1, Title = "Notif 1", Message = "Msg 1", CreatedAt = DateTime.UtcNow.AddHours(-2), IsRead = false },
            new UserNotification { RecipientUserId = 1, Title = "Notif 2", Message = "Msg 2", CreatedAt = DateTime.UtcNow.AddHours(-1), IsRead = true },
            new UserNotification { RecipientUserId = 2, Title = "Notif 3", Message = "Msg 3", CreatedAt = DateTime.UtcNow, IsRead = false }
        );
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.GetMyNotificationsAsync(1);

        Assert.Equal(2, result.Count);
        Assert.Equal("Notif 2", result[0].Title); // Most recent first
        Assert.Equal("Notif 1", result[1].Title);
        Assert.All(result, n => Assert.Equal(1, n.RecipientUserId));
    }

    [Fact]
    public async Task GetMyNotificationsAsync_NoNotifications_ReturnsEmptyList()
    {
        await using var ctx = BuildContext(nameof(GetMyNotificationsAsync_NoNotifications_ReturnsEmptyList));
        ctx.Users.Add(MakeUser(1));
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.GetMyNotificationsAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_NonExistentUser_ReturnsEmptyList()
    {
        await using var ctx = BuildContext(nameof(GetMyNotificationsAsync_NonExistentUser_ReturnsEmptyList));
        // No users added

        var svc = BuildService(ctx);
        var result = await svc.GetMyNotificationsAsync(999);

        Assert.Empty(result);
    }

    // ─── MarkAsReadAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAsReadAsync_OwnerCanMarkAsRead()
    {
        await using var ctx = BuildContext(nameof(MarkAsReadAsync_OwnerCanMarkAsRead));
        ctx.Users.Add(MakeUser(1));
        await ctx.SaveChangesAsync();

        var notif = new UserNotification { RecipientUserId = 1, Title = "Test", Message = "Msg", CreatedAt = DateTime.UtcNow, IsRead = false };
        ctx.UserNotifications.Add(notif);
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.MarkAsReadAsync(1, notif.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsRead);
        Assert.Equal("Notification marked as read.", result.Message);

        // Verify in DB
        var dbNotif = await ctx.UserNotifications.FindAsync(notif.Id);
        Assert.True(dbNotif!.IsRead);
    }

    [Fact]
    public async Task MarkAsReadAsync_AlreadyRead_ReturnsSuccessNoChange()
    {
        await using var ctx = BuildContext(nameof(MarkAsReadAsync_AlreadyRead_ReturnsSuccessNoChange));
        ctx.Users.Add(MakeUser(1));
        await ctx.SaveChangesAsync();

        var notif = new UserNotification { RecipientUserId = 1, Title = "Test", Message = "Msg", CreatedAt = DateTime.UtcNow, IsRead = true };
        ctx.UserNotifications.Add(notif);
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.MarkAsReadAsync(1, notif.Id);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsRead);
    }

    [Fact]
    public async Task MarkAsReadAsync_NonExistentNotification_ReturnsNotFound()
    {
        await using var ctx = BuildContext(nameof(MarkAsReadAsync_NonExistentNotification_ReturnsNotFound));
        ctx.Users.Add(MakeUser(1));
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.MarkAsReadAsync(1, 9999);

        Assert.False(result.Success);
        Assert.Equal(NotificationError.NotFound, result.Error);
        Assert.Equal("Notification not found.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task MarkAsReadAsync_OtherUserNotification_ReturnsUnauthorized()
    {
        await using var ctx = BuildContext(nameof(MarkAsReadAsync_OtherUserNotification_ReturnsUnauthorized));
        ctx.Users.Add(MakeUser(1));
        ctx.Users.Add(MakeUser(2));
        await ctx.SaveChangesAsync();

        var notif = new UserNotification { RecipientUserId = 2, Title = "Test", Message = "Msg", CreatedAt = DateTime.UtcNow, IsRead = false };
        ctx.UserNotifications.Add(notif);
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.MarkAsReadAsync(1, notif.Id);

        Assert.False(result.Success);
        Assert.Equal(NotificationError.Unauthorized, result.Error);
        Assert.Equal("You are not authorized to access this notification.", result.Message);
        Assert.Null(result.Data);

        // Verify notification unchanged in DB
        var dbNotif = await ctx.UserNotifications.FindAsync(notif.Id);
        Assert.False(dbNotif!.IsRead);
    }

    // ─── CreateNotificationAsync ────────────────────────────────────────────

    [Fact]
    public async Task CreateNotificationAsync_CreatesNotificationForUser()
    {
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_CreatesNotificationForUser));
        ctx.Users.Add(MakeUser(1));
        await ctx.SaveChangesAsync();

        var svc = BuildService(ctx);
        var result = await svc.CreateNotificationAsync(1, "New Title", "New Message");

        Assert.NotNull(result);
        Assert.Equal("New Title", result.Title);
        Assert.Equal("New Message", result.Message);
        Assert.Equal(1, result.RecipientUserId);
        Assert.False(result.IsRead);
        Assert.True(result.Id > 0);

        // Verify in DB
        var dbNotif = await ctx.UserNotifications.FindAsync(result.Id);
        Assert.NotNull(dbNotif);
        Assert.Equal("New Title", dbNotif!.Title);
    }

    [Fact]
    public async Task CreateNotificationAsync_NonExistentUser_CreatesOrphanNotification()
    {
        // InMemory DB doesn't enforce FK by default, so this tests the service behavior
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_NonExistentUser_CreatesOrphanNotification));
        // No user added

        var svc = BuildService(ctx);
        var result = await svc.CreateNotificationAsync(999, "Title", "Message");

        Assert.NotNull(result);
        Assert.Equal(999, result.RecipientUserId);
    }

    // ─── Notification Triggers from Other Services ──────────────────────────

    [Fact]
    public async Task CreateNotificationAsync_FromProposalSubmission_NotifiesTourist()
    {
        // Test that when a guide submits a proposal, the tourist gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromProposalSubmission_NotifiesTourist));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1);
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();

        var proposalService = new ProposalService(
            ctx,
            new LoggerFactory().CreateLogger<ProposalService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var submitDto = new SubmitProposalDto
        {
            Availability = "Available",
            Inclusions = "Hotel, meals",
            Exclusions = "Flights",
            Notes = "Bring sunscreen"
        };

        var result = await proposalService.SubmitProposalAsync(2, 1, 1, submitDto);

        Assert.True(result.Success);

        // Verify notification was created for the tourist
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 1)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("New proposal received", notifications[0].Title);
        Assert.Contains("Cox's Bazar", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromProposalWithdrawal_NotifiesTourist()
    {
        // Test that when a guide withdraws a proposal, the tourist gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromProposalWithdrawal_NotifiesTourist));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        ctx.TouristProfiles.Add(touristProfile);

        var tripRequest = MakeTripRequest(1, 1);
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();

        var proposalService = new ProposalService(
            ctx,
            new LoggerFactory().CreateLogger<ProposalService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var result = await proposalService.WithdrawProposalAsync(2, 1, 1);

        Assert.True(result.Success);

        // Verify notification was created for the tourist
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 1)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Proposal withdrawn", notifications[0].Title);
        Assert.Contains("Cox's Bazar", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromPaymentSuccess_NotifiesGuide()
    {
        // Test that when a payment for a proposal succeeds, the guide gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromPaymentSuccess_NotifiesGuide));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1, TripRequestStatus.Matched);
        tripRequest.MatchedGuideId = 2;
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();

        var paymentService = new PaymentService(
            ctx,
            new SandboxPaymentGateway(),
            new LoggerFactory().CreateLogger<PaymentService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var result = await paymentService.PayForProposalAsync(1, 1);

        Assert.True(result.Success);

        // Verify notification was created for the guide
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 2)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Payment received", notifications[0].Title);
        Assert.Contains("18,000", notifications[0].Message); // Amount in message
    }

    [Fact]
    public async Task CreateNotificationAsync_FromRegistrationPayment_NotifiesOrganizer()
    {
        // Test that when a registration payment succeeds, the organizer gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromRegistrationPayment_NotifiesOrganizer));
        var tourist = MakeUser(1);
        var organizer = MakeOrganizer(2);
        ctx.Users.AddRange(tourist, organizer);

        var touristProfile = MakeTouristProfile(1);
        var organizerProfile = MakeOrganizerProfile(2, 20);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.OrganizerProfiles.Add(organizerProfile);

        var trip = MakeOrganizedTrip(1, 20);
        ctx.OrganizedTrips.Add(trip);

        var registration = new Registration
        {
            Id = 1,
            OrganizedTripId = 1,
            TouristId = 1,
            NumberOfTravelers = 1,
            Status = RegistrationStatus.PendingPayment,
            TotalPrice = 15000m,
            RegisteredAt = DateTime.UtcNow
        };
        ctx.Registrations.Add(registration);
        await ctx.SaveChangesAsync();

        var paymentService = new PaymentService(
            ctx,
            new SandboxPaymentGateway(),
            new LoggerFactory().CreateLogger<PaymentService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var result = await paymentService.PayForRegistrationAsync(1, 1);

        Assert.True(result.Success);

        // Verify notification was created for the organizer
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 2)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Registration payment received", notifications[0].Title);
        Assert.Contains("15,000", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromEscrowRelease_NotifiesGuide()
    {
        // Test that when escrow is released, the guide gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromEscrowRelease_NotifiesGuide));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1, TripRequestStatus.Completed);
        tripRequest.MatchedGuideId = 2;
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);

        var payment = new Payment
        {
            Id = 1,
            TouristUserId = 1,
            TargetType = PaymentTargetType.Proposal,
            ProposalId = 1,
            Amount = 18000m,
            Currency = "USD",
            Status = PaymentStatus.Held,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        ctx.Payments.Add(payment);
        await ctx.SaveChangesAsync();

        var paymentService = new PaymentService(
            ctx,
            new SandboxPaymentGateway(),
            new LoggerFactory().CreateLogger<PaymentService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var result = await paymentService.ReleaseEscrowAsync(1, 1);

        Assert.True(result.Success);

        // Verify notification was created for the guide
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 2)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Escrow released", notifications[0].Title);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromMilestoneRelease_NotifiesGuide()
    {
        // Test that when a milestone is released, the guide gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromMilestoneRelease_NotifiesGuide));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1, TripRequestStatus.Completed);
        tripRequest.MatchedGuideId = 2;
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);

        var payment = new Payment
        {
            Id = 1,
            TouristUserId = 1,
            TargetType = PaymentTargetType.Proposal,
            ProposalId = 1,
            Amount = 18000m,
            Currency = "USD",
            Status = PaymentStatus.Held,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        ctx.Payments.Add(payment);

        var milestone = new PaymentMilestone
        {
            Id = 1,
            PaymentId = 1,
            Index = 1,
            Label = "First milestone",
            Amount = 9000m,
            Status = PaymentMilestoneStatus.Held,
            CreatedAt = DateTime.UtcNow,
            HeldAt = DateTime.UtcNow
        };
        ctx.PaymentMilestones.Add(milestone);
        await ctx.SaveChangesAsync();

        var paymentService = new PaymentService(
            ctx,
            new SandboxPaymentGateway(),
            new LoggerFactory().CreateLogger<PaymentService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var result = await paymentService.ReleaseMilestoneAsync(1, 1, 1);

        Assert.True(result.Success);

        // Verify notification was created for the guide
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 2)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Milestone released", notifications[0].Title);
        Assert.Contains("First milestone", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromRefund_NotifiesTourist()
    {
        // Test that when a refund is issued, the tourist gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromRefund_NotifiesTourist));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1);
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);

        var payment = new Payment
        {
            Id = 1,
            TouristUserId = 1,
            TargetType = PaymentTargetType.Proposal,
            ProposalId = 1,
            Amount = 18000m,
            Currency = "USD",
            Status = PaymentStatus.Held,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        ctx.Payments.Add(payment);
        await ctx.SaveChangesAsync();

        var paymentService = new PaymentService(
            ctx,
            new SandboxPaymentGateway(),
            new LoggerFactory().CreateLogger<PaymentService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        // Admin user (id 999) issues refund
        ctx.Users.Add(new User { Id = 999, FullName = "Admin", Email = "admin@test.com", PhoneNumber = "01700000999", PasswordHash = "hash", Role = UserRole.Admin, IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await paymentService.RefundAsync(999, 1, 9000m, "Partial refund");

        Assert.True(result.Success);

        // Verify notification was created for the tourist
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 1)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Refund issued", notifications[0].Title);
        Assert.Contains("9,000", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromDisputeRaised_NotifiesOtherParty()
    {
        // Test that when a dispute is raised, the other party gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromDisputeRaised_NotifiesOtherParty));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1);
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();

        var disputeService = new DisputeService(
            ctx,
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        // Tourist raises dispute
        var raiseDto = new RaiseDisputeDto
        {
            TripType = DisputeRelatedTripType.TripRequest,
            TripId = 1,
            Reason = "Guide didn't show up",
            Description = "Guide was late and didn't communicate"
        };

        // Debug: check if trip request exists
        var allTrips = await ctx.TripRequests.ToListAsync();
        var tripCheck = await ctx.TripRequests.FirstOrDefaultAsync(t => t.Id == 1);
        if (tripCheck == null)
        {
            throw new Exception($"Trip request not found in context before calling service! Total trips: {allTrips.Count}");
        }

        var result = await disputeService.RaiseDisputeAsync(1, UserRole.Tourist, raiseDto);

        // Verify notification was created for the guide (not the tourist who raised it)
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 2)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Dispute raised", notifications[0].Title);
        Assert.Contains("Guide didn't show up", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromItineraryAddDay_NotifiesTourist()
    {
        // Test that when a guide adds an itinerary day, the tourist gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromItineraryAddDay_NotifiesTourist));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1);
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();

        var itineraryService = new ItineraryService(
            ctx,
            new LoggerFactory().CreateLogger<ItineraryService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var createDayDto = new CreateItineraryDayDto
        {
            DayNumber = 1,
            Title = "Arrival Day",
            Activities = "Meet at hotel, explore city",
            Schedule = "09:00-18:00"
        };

        var result = await itineraryService.AddDayAsync(2, 1, createDayDto);

        Assert.True(result.Success);

        // Verify notification was created for the tourist
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 1)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Itinerary updated", notifications[0].Title);
        Assert.Contains("Arrival Day", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromItineraryUpdateDay_NotifiesTourist()
    {
        // Test that when a guide updates an itinerary day, the tourist gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromItineraryUpdateDay_NotifiesTourist));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1);
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);

        var day = new ItineraryDay
        {
            Id = 1,
            ProposalId = 1,
            DayNumber = 1,
            Title = "Original Title",
            Activities = "Original activities",
            Schedule = "09:00-18:00",
            CreatedAt = DateTime.UtcNow
        };
        ctx.ItineraryDays.Add(day);
        await ctx.SaveChangesAsync();

        var itineraryService = new ItineraryService(
            ctx,
            new LoggerFactory().CreateLogger<ItineraryService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var updateDayDto = new UpdateItineraryDayDto
        {
            Title = "Updated Title",
            Activities = "Updated activities",
            Schedule = "10:00-19:00"
        };

        var result = await itineraryService.UpdateDayAsync(2, 1, updateDayDto);

        Assert.True(result.Success);

        // Verify notification was created for the tourist
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 1)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Itinerary updated", notifications[0].Title);
        Assert.Contains("Day 1 (Updated Title)", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromItineraryDeleteDay_NotifiesTourist()
    {
        // Test that when a guide deletes an itinerary day, the tourist gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromItineraryDeleteDay_NotifiesTourist));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1);
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);

        var day = new ItineraryDay
        {
            Id = 1,
            ProposalId = 1,
            DayNumber = 1,
            Title = "Day to Delete",
            Activities = "Activities",
            Schedule = "09:00-18:00",
            CreatedAt = DateTime.UtcNow
        };
        ctx.ItineraryDays.Add(day);
        await ctx.SaveChangesAsync();

        var itineraryService = new ItineraryService(
            ctx,
            new LoggerFactory().CreateLogger<ItineraryService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var result = await itineraryService.DeleteDayAsync(2, 1);

        Assert.True(result.Success);

        // Verify notification was created for the tourist
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 1)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Itinerary updated", notifications[0].Title);
        Assert.Contains("Day 1 (Day to Delete)", notifications[0].Message);
    }

    [Fact]
    public async Task CreateNotificationAsync_FromItinerarySave_NotifiesTourist()
    {
        // Test that when a guide saves the full itinerary, the tourist gets notified
        await using var ctx = BuildContext(nameof(CreateNotificationAsync_FromItinerarySave_NotifiesTourist));
        var tourist = MakeUser(1);
        var guide = MakeGuide(2);
        ctx.Users.AddRange(tourist, guide);

        var touristProfile = MakeTouristProfile(1);
        var guideProfile = MakeGuideProfile(2);
        ctx.TouristProfiles.Add(touristProfile);
        ctx.GuideProfiles.Add(guideProfile);

        var tripRequest = MakeTripRequest(1, 1);
        ctx.TripRequests.Add(tripRequest);

        var proposal = new Proposal
        {
            Id = 1,
            GuideId = 2,
            TripRequestId = 1,
            ProposedPrice = 18000m,
            Message = "I can guide you",
            Status = ProposalStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Proposals.Add(proposal);
        await ctx.SaveChangesAsync();

        var itineraryService = new ItineraryService(
            ctx,
            new LoggerFactory().CreateLogger<ItineraryService>(),
            new NotificationService(ctx, new LoggerFactory().CreateLogger<NotificationService>(), new NoopNotificationSender()));

        var days = new List<CreateItineraryDayDto>
        {
            new() { DayNumber = 1, Title = "Day 1", Activities = "Activities 1", Schedule = "09:00-18:00" },
            new() { DayNumber = 2, Title = "Day 2", Activities = "Activities 2", Schedule = "09:00-18:00" }
        };

        var result = await itineraryService.SaveItineraryAsync(2, 1, days);

        Assert.True(result.Success);

        // Verify notification was created for the tourist
        var notifications = await ctx.UserNotifications
            .Where(n => n.RecipientUserId == 1)
            .ToListAsync();

        Assert.Single(notifications);
        Assert.Equal("Itinerary updated", notifications[0].Title);
        Assert.Equal("Your itinerary has been updated by the guide.", notifications[0].Message);
    }
}