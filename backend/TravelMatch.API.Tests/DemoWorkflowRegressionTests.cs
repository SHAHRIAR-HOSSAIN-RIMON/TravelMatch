using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Auth;
using TravelMatch.API.DTOs.Proposals;
using TravelMatch.API.DTOs.TripRequests;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

public class DemoWorkflowRegressionTests
{
    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static async Task<TripRequest> SeedTrip(ApplicationDbContext db)
    {
        db.Users.AddRange(new User { Id = 10, Role = UserRole.Tourist },
            new User { Id = 20, Role = UserRole.Guide, FullName = "Guide" });
        db.GuideProfiles.Add(new GuideProfile { Id = 99, UserId = 20, VerificationStatus = VerificationStatus.Verified });
        var trip = new TripRequest { Id = 30, TouristId = 10, MatchedGuideId = 20,
            Status = TripRequestStatus.PaymentPending, AcceptedPrice = 100 };
        db.TripRequests.Add(trip);
        db.Proposals.Add(new Proposal { Id = 40, TripRequestId = 30, GuideId = 20,
            ProposedPrice = 100, Status = ProposalStatus.Accepted });
        await db.SaveChangesAsync();
        return trip;
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task Planned_RejectsUnpaidOrFailedSandboxPayment(PaymentStatus? status)
    {
        await using var db = CreateDb();
        var trip = await SeedTrip(db);
        if (status.HasValue)
            db.Payments.Add(new Payment { TouristUserId = 10, ProposalId = 40, Amount = 100,
                TargetType = PaymentTargetType.Proposal, Status = status.Value });
        await db.SaveChangesAsync();
        var result = await new TripRequestService(db).TransitionToPlannedAsync(10, 30);
        Assert.False(result.Success);
        Assert.Equal(TripLifecycleError.PaymentNotCompleted, result.Error);
        Assert.Equal(TripRequestStatus.PaymentPending, trip.Status);
        Assert.Null(trip.PaymentCompletedAt);
    }

    [Theory]
    [InlineData(10, 100, true)]
    [InlineData(11, 100, false)]
    [InlineData(10, 99, false)]
    public async Task Planned_RequiresOwnedCorrectlyPricedDeposit(int payer, int amount, bool succeeds)
    {
        await using var db = CreateDb();
        var trip = await SeedTrip(db);
        var paidAt = DateTime.UtcNow.AddMinutes(-5);
        db.Payments.Add(new Payment { TouristUserId = payer, ProposalId = 40, Amount = amount,
            TargetType = PaymentTargetType.Proposal, Status = PaymentStatus.Held, CompletedAt = paidAt });
        await db.SaveChangesAsync();
        var result = await new TripRequestService(db).TransitionToPlannedAsync(10, 30);
        Assert.Equal(succeeds, result.Success);
        Assert.Equal(succeeds ? TripRequestStatus.Planned : TripRequestStatus.PaymentPending, trip.Status);
        Assert.Equal(succeeds ? paidAt : (DateTime?)null, trip.PaymentCompletedAt);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(1, false)]
    public async Task PaymentPending_UsesAcceptedQuote(int price, bool succeeds)
    {
        await using var db = CreateDb();
        var trip = await SeedTrip(db);
        trip.Status = TripRequestStatus.Matched;
        await db.SaveChangesAsync();
        var result = await new TripRequestService(db).TransitionToPaymentPendingAsync(10, 30, price);
        Assert.Equal(succeeds, result.Success);
        Assert.Equal(100m, trip.AcceptedPrice);
    }

    [Fact]
    public async Task AcceptedProposal_RemainsReadable_WithDistinctGuideProfileId()
    {
        await using var db = CreateDb();
        await SeedTrip(db);
        var notifications = new NotificationService(db, NullLogger<NotificationService>.Instance, new NoopNotificationSender());
        var service = new ProposalService(db, NullLogger<ProposalService>.Instance, notifications);
        var list = await service.GetProposalsAsync(10, 30, ProposalSort.Newest);
        Assert.Equal(40, Assert.Single(list.Data!).Id);
        var detail = await service.GetProposalDetailAsync(10, 30, 40);
        Assert.True(detail.Success);
        Assert.Equal(20, detail.Data!.Guide.Id);
        Assert.Equal(99, detail.Data.Guide.GuideProfileId);
        Assert.False(detail.Data.IsEligibleForAcceptance);
        Assert.False((await service.GetProposalDetailAsync(11, 30, 40)).Success);
    }

    [Fact]
    public async Task Login_NormalizesExistingMixedCaseEmail_AndRejectsInactiveAccount()
    {
        await using var db = CreateDb();
        var hasher = new PasswordHasher();
        db.Users.Add(new User { Id = 1, Email = "  Demo@Example.test ", PasswordHash = hasher.HashPassword("ExamplePassword123") });
        await db.SaveChangesAsync();
        var service = new AuthService(db, hasher, new TestTokens());
        var request = new LoginRequestDto { Email = "demo@example.test", Password = "ExamplePassword123" };
        Assert.True((await service.LoginAsync(request)).Success);
        db.Users.Local.Single().IsActive = false;
        await db.SaveChangesAsync();
        Assert.False((await service.LoginAsync(request)).Success);
        Assert.Null(await service.GetCurrentUserAsync(1));
    }

    [Fact]
    public async Task ExistingJwt_StopsWorkingAfterDeactivation()
    {
        await using var app = await TestApplication.StartAsync();
        await app.SeedAsync(db => { db.Users.Add(new User { Id = 10, Role = UserRole.Tourist }); return Task.CompletedTask; });
        app.UseToken(await app.CreateTokenAsync(10));
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/api/Profile/me")).StatusCode);
        await app.SeedAsync(async db => { (await db.Users.SingleAsync()).IsActive = false; });
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync("/api/Profile/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null)).StatusCode);
    }

    [Fact]
    public async Task Registration_NormalizesEmail_AndRejectsCaseInsensitiveDuplicate()
    {
        await using var db = CreateDb();
        var service = new AuthService(db, new PasswordHasher(), new TestTokens());
        var request = new RegisterRequestDto { FullName = "Demo Tourist", Email = " Demo@Example.test ",
            Password = "ExamplePassword123", PhoneNumber = "01700000000", Role = UserRole.Tourist };
        Assert.True((await service.RegisterAsync(request)).Success);
        Assert.Equal("demo@example.test", (await db.Users.SingleAsync()).Email);
        request.Email = "DEMO@EXAMPLE.TEST";
        var duplicate = await service.RegisterAsync(request);
        Assert.False(duplicate.Success);
        Assert.Equal(RegisterError.DuplicateEmail, duplicate.Error);
        Assert.Single(await db.Users.ToListAsync());
    }

    private sealed class TestTokens : IJwtTokenService
    {
        public string GenerateToken(User user) => "test-token";
    }
}
