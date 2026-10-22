using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Focused tests verifying that a successful dispute resolution sends the correct
/// notifications to the relevant participants, that notification failures do not
/// reverse a saved resolution, and that repeated resolution requests do not
/// generate duplicate notifications.
/// </summary>
public class DisputeResolutionNotificationTests
{
    // ─── Infrastructure helpers ───────────────────────────────────────────────

    private static ApplicationDbContext BuildContext(string name)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    /// <summary>
    /// Builds a DisputeService backed by a real NotificationService that
    /// uses the supplied real-time sender. Returned alongside the sender
    /// so tests can inspect what was sent.
    /// </summary>
    private static (DisputeService svc, NotificationService notificationSvc) BuildServices(
        ApplicationDbContext ctx,
        INotificationRealtimeSender sender)
    {
        var notificationSvc = new NotificationService(
            ctx,
            NullLogger<NotificationService>.Instance,
            sender);

        var disputeSvc = new DisputeService(ctx, notificationSvc);
        return (disputeSvc, notificationSvc);
    }

    private static DisputeService BuildDisputeService(
        ApplicationDbContext ctx,
        INotificationRealtimeSender sender) =>
        BuildServices(ctx, sender).svc;

    // ─── Seed helpers ─────────────────────────────────────────────────────────

    private static async Task<int> SeedAdminAsync(ApplicationDbContext ctx, int userId = 900)
    {
        ctx.Users.Add(new User
        {
            Id = userId, FullName = "Admin", Email = $"admin{userId}@t.com",
            PhoneNumber = $"0179{userId:00000}", Role = UserRole.Admin, IsActive = true
        });
        await ctx.SaveChangesAsync();
        return userId;
    }

    /// <summary>
    /// Seeds an OrganizedTrip dispute scenario:
    ///   - Organizer user (userId=1), OrganizerProfile (Id=10)
    ///   - Tourist user (userId=2), TouristProfile (Id=20)
    ///   - OrganizedTrip (Id=50, OrganizerId=10, SelectedGuideId=null unless guideUserId given)
    ///   - Registration (confirmed) linking tourist profile to the trip
    ///   - Open Dispute
    /// Returns the dispute id.
    /// </summary>
    private record OrganizedTripSeed(
        int OrganizerUserId, int TouristUserId, int TripId, int DisputeId,
        int? SelectedGuideUserId = null);

    private static async Task<OrganizedTripSeed> SeedOrganizedTripDisputeAsync(
        ApplicationDbContext ctx,
        int? selectedGuideUserId = null)
    {
        const int organizerUserId = 1;
        const int touristUserId = 2;
        const int guideUserId = 5;  // used only when selectedGuideUserId is passed
        const int tripId = 50;
        const int organizerProfileId = 10;
        const int touristProfileId = 20;

        ctx.Users.Add(new User
        {
            Id = organizerUserId, FullName = "Organizer", Email = "org@t.com",
            PhoneNumber = "01700000001", Role = UserRole.Organizer, IsActive = true
        });
        ctx.OrganizerProfiles.Add(new OrganizerProfile
        {
            Id = organizerProfileId, UserId = organizerUserId, GroupOrganizationName = "TravelOrg"
        });

        ctx.Users.Add(new User
        {
            Id = touristUserId, FullName = "Tourist", Email = "tourist@t.com",
            PhoneNumber = "01700000002", Role = UserRole.Tourist, IsActive = true
        });
        ctx.TouristProfiles.Add(new TouristProfile { Id = touristProfileId, UserId = touristUserId });

        if (selectedGuideUserId.HasValue)
        {
            ctx.Users.Add(new User
            {
                Id = guideUserId, FullName = "Guide", Email = "guide@t.com",
                PhoneNumber = "01700000005", Role = UserRole.Guide, IsActive = true
            });
        }

        ctx.OrganizedTrips.Add(new OrganizedTrip
        {
            Id = tripId, OrganizerId = organizerProfileId, Title = "Test Trip",
            Destination = "Sylhet", Description = "desc",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            MaxParticipants = 10, PricePerPerson = 200m, Inclusions = "Food",
            RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Status = OrganizedTripStatus.Completed,
            SelectedGuideId = selectedGuideUserId.HasValue ? guideUserId : null
        });

        ctx.Registrations.Add(new Registration
        {
            Id = 100, OrganizedTripId = tripId, TouristId = touristProfileId,
            NumberOfTravelers = 1, Status = RegistrationStatus.Confirmed,
            TotalPrice = 200m
        });

        var dispute = new Dispute
        {
            Id = 1, ReportedByUserId = touristUserId,
            TripType = DisputeRelatedTripType.OrganizedTrip, TripId = tripId,
            Reason = "Service not delivered", Description = "Details.",
            Status = DisputeStatus.Open, CreatedAt = DateTime.UtcNow
        };
        ctx.Disputes.Add(dispute);

        await ctx.SaveChangesAsync();

        return new OrganizedTripSeed(organizerUserId, touristUserId, tripId, dispute.Id,
            selectedGuideUserId.HasValue ? guideUserId : null);
    }

    /// <summary>
    /// Seeds a TripRequest dispute scenario with deliberately different User and
    /// TouristProfile ids:
    ///   - Tourist user (userId=3) with TouristProfile (Id=33)
    ///   - Unrelated tourist user (userId=99) whose TouristProfile has Id=3, colliding
    ///     with the User.Id stored in TripRequest.TouristId
    ///   - Guide user (userId=4)
    ///   - TripRequest (Id=300, TouristId=3 — a User.Id)
    ///   - Accepted Proposal (GuideId=4)
    ///   - Open Dispute (reported by tourist)
    /// </summary>
    private record TripRequestSeed(
        int TouristUserId, int GuideUserId, int TripRequestId, int DisputeId,
        int UnrelatedTouristUserId);

    private static async Task<TripRequestSeed> SeedTripRequestDisputeAsync(ApplicationDbContext ctx)
    {
        const int touristUserId = 3;
        const int guideUserId = 4;
        const int tripRequestId = 300;
        const int touristProfileId = 33;
        const int unrelatedTouristUserId = 99;
        const int collidingProfileId = touristUserId;

        ctx.Users.Add(new User
        {
            Id = touristUserId, FullName = "Tourist3", Email = "tourist3@t.com",
            PhoneNumber = "01700000003", Role = UserRole.Tourist, IsActive = true
        });
        ctx.TouristProfiles.Add(new TouristProfile { Id = touristProfileId, UserId = touristUserId });

        ctx.Users.Add(new User
        {
            Id = unrelatedTouristUserId, FullName = "Unrelated99", Email = "unrelated99@t.com",
            PhoneNumber = "01700000099", Role = UserRole.Tourist, IsActive = true
        });
        ctx.TouristProfiles.Add(new TouristProfile
        {
            Id = collidingProfileId, UserId = unrelatedTouristUserId
        });

        ctx.Users.Add(new User
        {
            Id = guideUserId, FullName = "Guide4", Email = "guide4@t.com",
            PhoneNumber = "01700000004", Role = UserRole.Guide, IsActive = true
        });

        ctx.TripRequests.Add(new TripRequest
        {
            Id = tripRequestId, TouristId = touristUserId,
            Destination = "Cox's Bazar", TripType = "Beach",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            NumberOfTravelers = 2, Budget = 500m, BudgetMin = 400m, BudgetMax = 700m,
            TravelPreferences = "Relaxed", Status = TripRequestStatus.Completed
        });

        ctx.Proposals.Add(new Proposal
        {
            Id = 400, TripRequestId = tripRequestId, GuideId = guideUserId,
            ProposedPrice = 500m, Availability = "Anytime",
            Inclusions = "All", Exclusions = "None",
            Status = ProposalStatus.Accepted, CreatedAt = DateTime.UtcNow
        });

        var dispute = new Dispute
        {
            Id = 2, ReportedByUserId = touristUserId,
            TripType = DisputeRelatedTripType.TripRequest, TripId = tripRequestId,
            Reason = "Guide no-show", Description = "Guide never arrived.",
            Status = DisputeStatus.Open, CreatedAt = DateTime.UtcNow
        };
        ctx.Disputes.Add(dispute);

        await ctx.SaveChangesAsync();

        return new TripRequestSeed(
            touristUserId, guideUserId, tripRequestId, dispute.Id, unrelatedTouristUserId);
    }

    // ─── Tests: OrganizedTrip dispute notifications ───────────────────────────

    [Fact]
    public async Task Resolve_OrganizedTripDispute_NotifiesOrganizerAndTourist()
    {
        await using var ctx = BuildContext(nameof(Resolve_OrganizedTripDispute_NotifiesOrganizerAndTourist));
        var adminId = await SeedAdminAsync(ctx);
        var seed = await SeedOrganizedTripDisputeAsync(ctx);
        var sender = new RecordingNotificationSender();
        var svc = BuildDisputeService(ctx, sender);

        var result = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, "Organizer issued a credit.");

        Assert.True(result.Success, result.Message);

        var recipientIds = sender.Sent.Select(n => n.RecipientUserId).ToHashSet();
        Assert.Contains(seed.OrganizerUserId, recipientIds);
        Assert.Contains(seed.TouristUserId, recipientIds);
    }

    [Fact]
    public async Task Resolve_OrganizedTripDispute_WithSelectedGuide_NotifiesGuideAsWell()
    {
        await using var ctx = BuildContext(nameof(Resolve_OrganizedTripDispute_WithSelectedGuide_NotifiesGuideAsWell));
        var adminId = await SeedAdminAsync(ctx);
        var seed = await SeedOrganizedTripDisputeAsync(ctx, selectedGuideUserId: 5);
        var sender = new RecordingNotificationSender();
        var svc = BuildDisputeService(ctx, sender);

        var result = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, "Both sides compensated.");

        Assert.True(result.Success, result.Message);

        var recipientIds = sender.Sent.Select(n => n.RecipientUserId).ToHashSet();
        Assert.Contains(seed.OrganizerUserId, recipientIds);
        Assert.Contains(seed.TouristUserId, recipientIds);
        Assert.Contains(seed.SelectedGuideUserId!.Value, recipientIds);
    }

    [Fact]
    public async Task Resolve_OrganizedTripDispute_AdminIsNotNotified()
    {
        await using var ctx = BuildContext(nameof(Resolve_OrganizedTripDispute_AdminIsNotNotified));
        var adminId = await SeedAdminAsync(ctx);
        var seed = await SeedOrganizedTripDisputeAsync(ctx);
        var sender = new RecordingNotificationSender();
        var svc = BuildDisputeService(ctx, sender);

        var result = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, "Case closed.");

        Assert.True(result.Success, result.Message);
        Assert.DoesNotContain(adminId, sender.Sent.Select(n => n.RecipientUserId));
    }

    // ─── Tests: TripRequest dispute notifications ─────────────────────────────

    [Fact]
    public async Task Resolve_TripRequestDispute_NotifiesTouristAndGuide()
    {
        await using var ctx = BuildContext(nameof(Resolve_TripRequestDispute_NotifiesTouristAndGuide));
        var adminId = await SeedAdminAsync(ctx);
        var seed = await SeedTripRequestDisputeAsync(ctx);
        var sender = new RecordingNotificationSender();
        var svc = BuildDisputeService(ctx, sender);

        var result = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, "Refund issued to tourist.");

        Assert.True(result.Success, result.Message);

        var recipientIds = sender.Sent.Select(n => n.RecipientUserId).ToHashSet();
        Assert.Contains(seed.TouristUserId, recipientIds);
        Assert.Contains(seed.GuideUserId, recipientIds);
        Assert.DoesNotContain(seed.UnrelatedTouristUserId, recipientIds);
    }

    [Fact]
    public async Task Resolve_TripRequestDispute_NotificationFailure_DoesNotReversePersistedResolution()
    {
        await using var ctx = BuildContext(nameof(Resolve_TripRequestDispute_NotificationFailure_DoesNotReversePersistedResolution));
        var adminId = await SeedAdminAsync(ctx);
        var seed = await SeedTripRequestDisputeAsync(ctx);
        var svc = BuildDisputeService(ctx, new ThrowingNotificationSender());

        var result = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, "Resolved despite hub failure.");

        Assert.True(result.Success, result.Message);
        Assert.Equal(nameof(DisputeStatus.Resolved), result.Data!.Status);

        var saved = await ctx.Disputes.FirstAsync(d => d.Id == seed.DisputeId);
        Assert.Equal(DisputeStatus.Resolved, saved.Status);
        Assert.Equal("Resolved despite hub failure.", saved.ResolutionNote);
        Assert.Equal(adminId, saved.ResolvedByUserId);
        Assert.NotNull(saved.ResolvedAt);
    }

    [Fact]
    public async Task Raise_GuideReportedTripRequestDispute_NotifiesTouristUserNotProfileIdCollision()
    {
        await using var ctx = BuildContext(nameof(Raise_GuideReportedTripRequestDispute_NotifiesTouristUserNotProfileIdCollision));
        var seed = await SeedTripRequestDisputeAsync(ctx);
        var sender = new RecordingNotificationSender();
        var svc = BuildDisputeService(ctx, sender);

        var result = await svc.RaiseDisputeAsync(seed.GuideUserId, UserRole.Guide, new RaiseDisputeDto
        {
            TripType = DisputeRelatedTripType.TripRequest,
            TripId = seed.TripRequestId,
            Reason = "Tourist unreachable",
            Description = "The tourist did not respond before departure."
        });

        Assert.True(result.Success, result.Message);

        var recipientIds = sender.Sent.Select(n => n.RecipientUserId).ToHashSet();
        Assert.Contains(seed.TouristUserId, recipientIds);
        Assert.DoesNotContain(seed.UnrelatedTouristUserId, recipientIds);
    }

    // ─── Tests: notification content ─────────────────────────────────────────

    [Fact]
    public async Task Resolve_Notification_IdentifiesDisputeIdAndResolutionOutcome()
    {
        await using var ctx = BuildContext(nameof(Resolve_Notification_IdentifiesDisputeIdAndResolutionOutcome));
        var adminId = await SeedAdminAsync(ctx);
        var seed = await SeedOrganizedTripDisputeAsync(ctx);
        var sender = new RecordingNotificationSender();
        var svc = BuildDisputeService(ctx, sender);

        const string resolutionNote = "Both parties agreed to a 50% refund.";
        var result = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, resolutionNote);

        Assert.True(result.Success, result.Message);
        Assert.True(sender.Sent.Count > 0, "Expected at least one notification to be sent.");

        // Every notification must reference the dispute id and the resolution note.
        foreach (var notification in sender.Sent)
        {
            Assert.Contains(seed.DisputeId.ToString(), notification.Message);
            Assert.Contains(resolutionNote, notification.Message);
            // Title signals that this is a resolution event, not a raise event.
            Assert.Equal("Dispute resolved", notification.Title);
        }
    }

    // ─── Tests: notification failure resilience ───────────────────────────────

    [Fact]
    public async Task Resolve_NotificationDeliveryFailure_DoesNotReversePersistedResolution()
    {
        await using var ctx = BuildContext(nameof(Resolve_NotificationDeliveryFailure_DoesNotReversePersistedResolution));
        var adminId = await SeedAdminAsync(ctx);
        var seed = await SeedOrganizedTripDisputeAsync(ctx);
        // ThrowingNotificationSender throws on every SendToUserAsync call, which
        // tests that a real-time hub failure cannot undo the resolution.
        var svc = BuildDisputeService(ctx, new ThrowingNotificationSender());

        var result = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, "Resolved despite hub failure.");

        Assert.True(result.Success, result.Message);
        Assert.Equal(nameof(DisputeStatus.Resolved), result.Data!.Status);

        // Persisted state must reflect the resolution regardless of the notification failure.
        var saved = await ctx.Disputes.FirstAsync(d => d.Id == seed.DisputeId);
        Assert.Equal(DisputeStatus.Resolved, saved.Status);
        Assert.Equal("Resolved despite hub failure.", saved.ResolutionNote);
        Assert.Equal(adminId, saved.ResolvedByUserId);
        Assert.NotNull(saved.ResolvedAt);
    }

    // ─── Tests: no duplicate notifications on repeated resolution ─────────────

    [Fact]
    public async Task Resolve_Twice_SecondCallReturnsDuplicateError_AndNoNewNotificationsAreSent()
    {
        await using var ctx = BuildContext(nameof(Resolve_Twice_SecondCallReturnsDuplicateError_AndNoNewNotificationsAreSent));
        var adminId = await SeedAdminAsync(ctx);
        var seed = await SeedOrganizedTripDisputeAsync(ctx);
        var sender = new RecordingNotificationSender();
        var svc = BuildDisputeService(ctx, sender);

        // First resolution — succeeds and sends notifications.
        var first = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, "First note.");
        Assert.True(first.Success, first.Message);
        int countAfterFirst = sender.Sent.Count;
        Assert.True(countAfterFirst > 0, "Expected notifications after the first resolution.");

        // Second attempt on the same dispute — must be rejected because the dispute is no longer Open.
        var second = await svc.ResolveDisputeAsync(adminId, seed.DisputeId, "Attempted duplicate.");
        Assert.False(second.Success);
        Assert.Equal(DisputeError.DisputeAlreadyResolved, second.Error);

        // No additional notifications should have been sent.
        Assert.Equal(countAfterFirst, sender.Sent.Count);
    }
}
