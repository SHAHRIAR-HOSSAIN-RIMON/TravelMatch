using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Controllers;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

public class PaymentTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class RecordingPaymentGateway : ISandboxPaymentGateway
    {
        public List<decimal> ChargedAmounts { get; } = new();

        public Task<PaymentSimulationResult> ProcessPaymentAsync(
            decimal amount, string currency, string? paymentMethod = null)
        {
            ChargedAmounts.Add(amount);
            return Task.FromResult(new PaymentSimulationResult
            {
                IsSuccess = true,
                TransactionReference = $"TEST-TXN-{ChargedAmounts.Count}"
            });
        }
    }

    private static (PaymentService service, ApplicationDbContext context, ISandboxPaymentGateway gateway) CreatePaymentService(
        ApplicationDbContext? context = null,
        ISandboxPaymentGateway? gateway = null)
    {
        var db = context ?? CreateInMemoryDbContext();
        var gw = gateway ?? new SandboxPaymentGateway();
        var logger = NullLogger<PaymentService>.Instance;
        var notificationService = new NotificationService(db, NullLogger<NotificationService>.Instance, new NoopNotificationSender());
        var service = new PaymentService(db, gw, logger, notificationService);
        return (service, db, gw);
    }

    private static async Task<(User tourist, User guide, TripRequest tripRequest, Proposal proposal)> SeedAcceptedProposalAsync(
        ApplicationDbContext context,
        decimal proposedPrice = 500m)
    {
        var tourist = new User
        {
            Id = 1,
            FullName = "Alice Tourist",
            Email = "alice@example.com",
            PhoneNumber = "01711111111",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var guide = new User
        {
            Id = 2,
            FullName = "Bob Guide",
            Email = "bob@example.com",
            PhoneNumber = "01722222222",
            Role = UserRole.Guide,
            IsActive = true
        };

        var tripRequest = new TripRequest
        {
            Id = 10,
            TouristId = tourist.Id,
            Destination = "Cox's Bazar",
            TripType = "Beach",
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 5),
            NumberOfTravelers = 2,
            Budget = 600m,
            BudgetMin = 400m,
            BudgetMax = 700m,
            TravelPreferences = "Relaxation",
            Status = TripRequestStatus.Matched,
            MatchedGuideId = guide.Id
        };

        var proposal = new Proposal
        {
            Id = 100,
            TripRequestId = tripRequest.Id,
            TripRequest = tripRequest,
            GuideId = guide.Id,
            ProposedPrice = proposedPrice,
            Status = ProposalStatus.Accepted
        };

        context.Users.AddRange(tourist, guide);
        context.TripRequests.Add(tripRequest);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        return (tourist, guide, tripRequest, proposal);
    }

    private static async Task<(User tourist, TouristProfile profile, OrganizedTrip trip, Registration registration)> SeedRegistrationAsync(
        ApplicationDbContext context,
        decimal pricePerPerson = 250m,
        int maxParticipants = 5,
        int existingConfirmed = 0,
        int[]? existingConfirmedTravelers = null,
        RegistrationStatus status = RegistrationStatus.PendingPayment,
        OrganizedTripStatus tripStatus = OrganizedTripStatus.RegistrationOpen,
        int daysUntilDeadline = 5,
        int numberOfTravelers = 1,
        decimal? totalPrice = null)
    {
        var organizerUser = new User
        {
            Id = 20,
            FullName = "Org Leader",
            Email = "org@example.com",
            PhoneNumber = "01733333333",
            Role = UserRole.Organizer,
            IsActive = true
        };

        var organizerProfile = new OrganizerProfile
        {
            Id = 200,
            UserId = organizerUser.Id,
            GroupOrganizationName = "Adventure Trips"
        };

        var touristUser = new User
        {
            Id = 30,
            FullName = "Tom Tourist",
            Email = "tom@example.com",
            PhoneNumber = "01744444444",
            Role = UserRole.Tourist,
            IsActive = true
        };

        var touristProfile = new TouristProfile
        {
            Id = 300,
            UserId = touristUser.Id,
            Preferences = "Mountains"
        };

        var trip = new OrganizedTrip
        {
            Id = 500,
            OrganizerId = organizerProfile.Id,
            Title = "Bandarban Trek",
            Destination = "Bandarban",
            Description = "Hiking tour",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
            MaxParticipants = maxParticipants,
            PricePerPerson = pricePerPerson,
            Inclusions = "Food, tent",
            RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysUntilDeadline)),
            Status = tripStatus
        };

        var registration = new Registration
        {
            Id = 800,
            OrganizedTripId = trip.Id,
            TouristId = touristProfile.Id,
            NumberOfTravelers = numberOfTravelers,
            TotalPrice = totalPrice ?? pricePerPerson * numberOfTravelers,
            RegisteredAt = DateTime.UtcNow,
            Status = status
        };

        context.Users.AddRange(organizerUser, touristUser);
        context.OrganizerProfiles.Add(organizerProfile);
        context.TouristProfiles.Add(touristProfile);
        context.OrganizedTrips.Add(trip);
        context.Registrations.Add(registration);

        // Seed any other confirmed registrations to test capacity
        var otherTravelerCounts = existingConfirmedTravelers ?? Enumerable.Repeat(1, existingConfirmed).ToArray();
        for (int i = 0; i < otherTravelerCounts.Length; i++)
        {
            var otherTouristUser = new User
            {
                Id = 1001 + i,
                FullName = $"Other Tourist {i + 1}",
                Email = $"other{i + 1}@example.com",
                PhoneNumber = $"0179999990{i + 1}",
                Role = UserRole.Tourist,
                IsActive = true
            };
            var otherProfile = new TouristProfile
            {
                Id = 2001 + i,
                UserId = otherTouristUser.Id
            };
            var otherReg = new Registration
            {
                Id = 3001 + i,
                OrganizedTripId = trip.Id,
                TouristId = otherProfile.Id,
                NumberOfTravelers = otherTravelerCounts[i],
                Status = RegistrationStatus.Confirmed,
                RegisteredAt = DateTime.UtcNow
            };
            context.Users.Add(otherTouristUser);
            context.TouristProfiles.Add(otherProfile);
            context.Registrations.Add(otherReg);
        }

        await context.SaveChangesAsync();

        return (touristUser, touristProfile, trip, registration);
    }

    [Fact]
    public async Task PayProposal_Success_SetsPaymentHeldInEscrowAndStoresAuthoritativeAmount()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, proposal) = await SeedAcceptedProposalAsync(context, proposedPrice: 750m);

        var result = await service.PayForProposalAsync(tourist.Id, proposal.Id);

        Assert.True(result.Success);
        Assert.Equal(PaymentError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal(PaymentStatus.Held, result.Data.Status);
        Assert.Equal(750m, result.Data.Amount);
        Assert.NotEmpty(result.Data.TransactionReference);

        var savedPayment = await context.Payments.FirstOrDefaultAsync(p => p.ProposalId == proposal.Id);
        Assert.NotNull(savedPayment);
        Assert.Equal(PaymentStatus.Held, savedPayment.Status);
        Assert.Equal(750m, savedPayment.Amount);
        Assert.NotNull(savedPayment.CompletedAt);
    }

    [Fact]
    public async Task PayProposal_SimulatedFailure_LeavesPaymentFailedAndAllowsRetry()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, proposal) = await SeedAcceptedProposalAsync(context, proposedPrice: 500m);

        // Simulate gateway decline using payment method "FAIL"
        var failedResult = await service.PayForProposalAsync(
            tourist.Id,
            proposal.Id,
            new PayProposalRequestDto { PaymentMethod = "FAIL" });

        Assert.False(failedResult.Success);
        Assert.Equal(PaymentError.PaymentFailed, failedResult.Error);
        Assert.NotNull(failedResult.Data);
        Assert.Equal(PaymentStatus.Failed, failedResult.Data.Status);
        Assert.NotNull(failedResult.Data.FailureReason);

        // Proposal state remains Accepted and retryable
        var proposalInDb = await context.Proposals.FindAsync(proposal.Id);
        Assert.NotNull(proposalInDb);
        Assert.Equal(ProposalStatus.Accepted, proposalInDb.Status);

        // Now retry with normal sandbox method -> must succeed and enter escrow
        var retryResult = await service.PayForProposalAsync(tourist.Id, proposal.Id);
        Assert.True(retryResult.Success);
        Assert.Equal(PaymentError.None, retryResult.Error);
        Assert.Equal(PaymentStatus.Held, retryResult.Data!.Status);

        // Verify database contains 1 failed and 1 held payment
        var totalPayments = await context.Payments.Where(p => p.ProposalId == proposal.Id).ToListAsync();
        Assert.Equal(2, totalPayments.Count);
        Assert.Contains(totalPayments, p => p.Status == PaymentStatus.Failed);
        Assert.Contains(totalPayments, p => p.Status == PaymentStatus.Held);
    }

    [Fact]
    public async Task PayProposal_DuplicatePayment_FailsWithAlreadyPaidConflict()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, proposal) = await SeedAcceptedProposalAsync(context, proposedPrice: 300m);

        // Initial payment succeeds
        var firstResult = await service.PayForProposalAsync(tourist.Id, proposal.Id);
        Assert.True(firstResult.Success);

        // Duplicate payment attempt
        var secondResult = await service.PayForProposalAsync(tourist.Id, proposal.Id);
        Assert.False(secondResult.Success);
        Assert.Equal(PaymentError.AlreadyPaid, secondResult.Error);
        Assert.Equal("Payment has already been completed for this proposal.", secondResult.Message);
    }

    [Fact]
    public async Task PayProposal_UnauthorizedTourist_ReturnsForbiddenError()
    {
        var (service, context, _) = CreatePaymentService();
        var (_, _, _, proposal) = await SeedAcceptedProposalAsync(context);

        const int differentTouristUserId = 999;
        var result = await service.PayForProposalAsync(differentTouristUserId, proposal.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task PayProposal_NonAcceptedStatus_FailsWithInvalidProposalStatus()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, proposal) = await SeedAcceptedProposalAsync(context);

        proposal.Status = ProposalStatus.Submitted; // not yet accepted
        await context.SaveChangesAsync();

        var result = await service.PayForProposalAsync(tourist.Id, proposal.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.InvalidProposalStatus, result.Error);
    }

    [Fact]
    public async Task PayRegistration_Success_ConfirmsRegistrationAndSavesPayment()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, registration) = await SeedRegistrationAsync(context, pricePerPerson: 400m);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.True(result.Success);
        Assert.Equal(PaymentError.None, result.Error);
        Assert.NotNull(result.Data);
        Assert.Equal(PaymentStatus.Held, result.Data.Status);
        Assert.Equal(400m, result.Data.Amount);

        // Registration status must be confirmed
        var regInDb = await context.Registrations.FindAsync(registration.Id);
        Assert.NotNull(regInDb);
        Assert.Equal(RegistrationStatus.Confirmed, regInDb.Status);
    }

    [Fact]
    public async Task PayRegistration_OneTraveler_ChargesRegistrationTotal()
    {
        var gateway = new RecordingPaymentGateway();
        var (service, context, _) = CreatePaymentService(gateway: gateway);
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context, pricePerPerson: 400m, numberOfTravelers: 1);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.True(result.Success, result.Message);
        Assert.Equal(400m, registration.TotalPrice);
        Assert.Equal(400m, result.Data!.Amount);
        Assert.Equal(new[] { 400m }, gateway.ChargedAmounts);
    }

    [Fact]
    public async Task PayRegistration_MultiTraveler_ChargesFullRegistrationTotal()
    {
        var gateway = new RecordingPaymentGateway();
        var (service, context, _) = CreatePaymentService(gateway: gateway);
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context, pricePerPerson: 250m, numberOfTravelers: 3);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.True(result.Success, result.Message);
        Assert.Equal(750m, registration.TotalPrice);
        Assert.Equal(750m, result.Data!.Amount);
        Assert.Equal(new[] { 750m }, gateway.ChargedAmounts);

        var saved = await context.Registrations.FindAsync(registration.Id);
        Assert.Equal(RegistrationStatus.Confirmed, saved!.Status);
    }

    [Fact]
    public async Task PayRegistration_InconsistentTotal_IsRejectedWithoutCharging()
    {
        var gateway = new RecordingPaymentGateway();
        var (service, context, _) = CreatePaymentService(gateway: gateway);
        // Two travelers at 250 each should total 500; a stale total of 250 must not be charged.
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context, pricePerPerson: 250m, numberOfTravelers: 2, totalPrice: 250m);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.InvalidRequest, result.Error);
        Assert.Contains("500.00", result.Message);
        Assert.Empty(gateway.ChargedAmounts);
        Assert.Empty(context.Payments);

        var saved = await context.Registrations.FindAsync(registration.Id);
        Assert.Equal(RegistrationStatus.PendingPayment, saved!.Status);
    }

    [Fact]
    public async Task PayRegistration_MultiTraveler_RepeatedAttempts_ChargeOnlyOnce()
    {
        var gateway = new RecordingPaymentGateway();
        var (service, context, _) = CreatePaymentService(gateway: gateway);
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context, pricePerPerson: 250m, numberOfTravelers: 2);

        var first = await service.PayForRegistrationAsync(tourist.Id, registration.Id);
        var second = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.True(first.Success, first.Message);
        Assert.Equal(500m, first.Data!.Amount);
        Assert.False(second.Success);
        Assert.Equal(PaymentError.AlreadyPaid, second.Error);

        Assert.Single(gateway.ChargedAmounts);
        Assert.Equal(500m, gateway.ChargedAmounts[0]);
        Assert.Single(context.Payments);
    }

    [Fact]
    public async Task PayRegistration_MultiTraveler_MilestonePlanMatchesFullTotal()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context, pricePerPerson: 250m, numberOfTravelers: 2);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id,
            new PayRegistrationRequestDto
            {
                Milestones = new List<CreatePaymentMilestoneDto>
                {
                    new() { Index = 1, Label = "Deposit", Amount = 200m },
                    new() { Index = 2, Label = "Balance", Amount = 300m }
                }
            });

        Assert.True(result.Success, result.Message);
        Assert.Equal(500m, result.Data!.Amount);
        Assert.Equal(2, result.Data.Milestones.Count);
        Assert.Equal(500m, result.Data.Milestones.Sum(m => m.Amount));
    }

    [Fact]
    public async Task PayRegistration_SimulatedFailure_LeavesRegistrationPendingPaymentAndAllowsRetry()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, registration) = await SeedRegistrationAsync(context);

        // Simulate failure
        var failedResult = await service.PayForRegistrationAsync(
            tourist.Id,
            registration.Id,
            new PayRegistrationRequestDto { PaymentMethod = "FAIL" });

        Assert.False(failedResult.Success);
        Assert.Equal(PaymentError.PaymentFailed, failedResult.Error);

        // Registration remains PendingPayment
        var regInDb = await context.Registrations.FindAsync(registration.Id);
        Assert.NotNull(regInDb);
        Assert.Equal(RegistrationStatus.PendingPayment, regInDb.Status);

        // Retry succeeds and confirms registration
        var retryResult = await service.PayForRegistrationAsync(tourist.Id, registration.Id);
        Assert.True(retryResult.Success);

        var confirmedReg = await context.Registrations.FindAsync(registration.Id);
        Assert.NotNull(confirmedReg);
        Assert.Equal(RegistrationStatus.Confirmed, confirmedReg.Status);
    }

    [Fact]
    public async Task PayRegistration_CapacityExceeded_RejectsPayment()
    {
        var (service, context, _) = CreatePaymentService();
        // Trip has max 2 participants, and already 2 confirmed registrations
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context,
            maxParticipants: 2,
            existingConfirmed: 2);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.TripCapacityExceeded, result.Error);

        // Registration remains PendingPayment and no payment is saved
        var regInDb = await context.Registrations.FindAsync(registration.Id);
        Assert.NotNull(regInDb);
        Assert.Equal(RegistrationStatus.PendingPayment, regInDb.Status);
    }

    [Fact]
    public async Task PayRegistration_SumsTravelersAcrossConfirmedRegistrations_RejectsExceedingCapacityByOne()
    {
        var context = CreateInMemoryDbContext();
        var gateway = new RecordingPaymentGateway();
        var (service, _, _) = CreatePaymentService(context, gateway);

        // Two confirmed registrations: 1 + 2 = 3 travelers. A 3-traveler payment
        // would reach 6 travelers against a maximum of 5 (one over).
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context,
            maxParticipants: 5,
            existingConfirmedTravelers: new[] { 1, 2 },
            numberOfTravelers: 3);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.TripCapacityExceeded, result.Error);
        Assert.Empty(gateway.ChargedAmounts);
        Assert.Equal(0, await context.Payments.CountAsync(p => p.RegistrationId == registration.Id));

        var stored = await context.Registrations.FindAsync(registration.Id);
        Assert.NotNull(stored);
        Assert.Equal(RegistrationStatus.PendingPayment, stored.Status);
        Assert.Null(stored.ConfirmedAt);
    }

    [Fact]
    public async Task PayRegistration_ExactlyFillsRemainingCapacity_Succeeds()
    {
        var context = CreateInMemoryDbContext();
        var gateway = new RecordingPaymentGateway();
        var (service, _, _) = CreatePaymentService(context, gateway);

        // Two confirmed registrations: 2 + 3 = 5 travelers. Three more exactly fill the trip.
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context,
            maxParticipants: 8,
            existingConfirmedTravelers: new[] { 2, 3 },
            numberOfTravelers: 3);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(750m, result.Data!.Amount);
        Assert.Equal(PaymentStatus.Held, result.Data.Status);
        Assert.Equal(new[] { 750m }, gateway.ChargedAmounts);

        var payment = await context.Payments.SingleAsync(p => p.RegistrationId == registration.Id);
        Assert.Equal(750m, payment.Amount);
        Assert.Equal(PaymentStatus.Held, payment.Status);
        Assert.NotNull(payment.CompletedAt);

        var stored = await context.Registrations.FindAsync(registration.Id);
        Assert.NotNull(stored);
        Assert.Equal(RegistrationStatus.Confirmed, stored.Status);
        Assert.NotNull(stored.ConfirmedAt);
    }

    [Fact]
    public async Task PayRegistration_MultiTraveler_WhenTripAlreadyFull_Rejects()
    {
        var context = CreateInMemoryDbContext();
        var gateway = new RecordingPaymentGateway();
        var (service, _, _) = CreatePaymentService(context, gateway);

        // Confirmed 2 + 3 travelers already fill the trip to its maximum of 5.
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context,
            maxParticipants: 5,
            existingConfirmedTravelers: new[] { 2, 3 },
            numberOfTravelers: 2);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.TripCapacityExceeded, result.Error);
        Assert.Empty(gateway.ChargedAmounts);
        Assert.Equal(0, await context.Payments.CountAsync(p => p.RegistrationId == registration.Id));

        var stored = await context.Registrations.FindAsync(registration.Id);
        Assert.NotNull(stored);
        Assert.Equal(RegistrationStatus.PendingPayment, stored.Status);
        Assert.Null(stored.ConfirmedAt);
    }

    [Fact]
    public async Task PayRegistration_RepeatedAttempts_DoNotDoubleCountTravelers()
    {
        var context = CreateInMemoryDbContext();
        var gateway = new RecordingPaymentGateway();
        var (service, _, _) = CreatePaymentService(context, gateway);

        var (tourist, _, trip, registration) = await SeedRegistrationAsync(
            context,
            maxParticipants: 5,
            numberOfTravelers: 3);

        var first = await service.PayForRegistrationAsync(tourist.Id, registration.Id);
        Assert.True(first.Success);

        var repeated = await service.PayForRegistrationAsync(tourist.Id, registration.Id);
        Assert.False(repeated.Success);
        Assert.Equal(PaymentError.AlreadyPaid, repeated.Error);

        // A second tourist registers 2 travelers. The confirmed 3 travelers must be
        // counted once, so 3 + 2 exactly fills the trip; double counting would reject.
        var secondTouristUser = new User
        {
            Id = 31,
            FullName = "Second Tourist",
            Email = "second@example.com",
            PhoneNumber = "01755555555",
            Role = UserRole.Tourist,
            IsActive = true
        };
        var secondProfile = new TouristProfile { Id = 301, UserId = secondTouristUser.Id };
        var secondRegistration = new Registration
        {
            Id = 801,
            OrganizedTripId = trip.Id,
            TouristId = secondProfile.Id,
            NumberOfTravelers = 2,
            TotalPrice = 500m,
            Status = RegistrationStatus.PendingPayment,
            RegisteredAt = DateTime.UtcNow
        };

        context.Users.Add(secondTouristUser);
        context.TouristProfiles.Add(secondProfile);
        context.Registrations.Add(secondRegistration);
        await context.SaveChangesAsync();

        var second = await service.PayForRegistrationAsync(secondTouristUser.Id, secondRegistration.Id);

        Assert.True(second.Success);
        Assert.Equal(new[] { 750m, 500m }, gateway.ChargedAmounts);
        Assert.Equal(2, await context.Payments.CountAsync(p =>
            p.RegistrationId == registration.Id || p.RegistrationId == secondRegistration.Id));

        var firstStored = await context.Registrations.FindAsync(registration.Id);
        var secondStored = await context.Registrations.FindAsync(secondRegistration.Id);
        Assert.Equal(RegistrationStatus.Confirmed, firstStored!.Status);
        Assert.Equal(RegistrationStatus.Confirmed, secondStored!.Status);
    }

    [Fact]
    public async Task PayRegistration_DuplicatePayment_FailsWithAlreadyPaidConflict()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, registration) = await SeedRegistrationAsync(context);

        var first = await service.PayForRegistrationAsync(tourist.Id, registration.Id);
        Assert.True(first.Success);

        var second = await service.PayForRegistrationAsync(tourist.Id, registration.Id);
        Assert.False(second.Success);
        Assert.Equal(PaymentError.AlreadyPaid, second.Error);
    }

    [Fact]
    public async Task PayRegistration_UnauthorizedTourist_ReturnsForbidden()
    {
        var (service, context, _) = CreatePaymentService();
        var (_, _, _, registration) = await SeedRegistrationAsync(context);

        // Seed a distinct tourist with their own profile
        var differentTouristUser = new User
        {
            Id = 888,
            FullName = "Different Tourist",
            Email = "diff@example.com",
            PhoneNumber = "01788888888",
            Role = UserRole.Tourist,
            IsActive = true
        };
        var differentProfile = new TouristProfile
        {
            Id = 8880,
            UserId = differentTouristUser.Id
        };
        context.Users.Add(differentTouristUser);
        context.TouristProfiles.Add(differentProfile);
        await context.SaveChangesAsync();

        var result = await service.PayForRegistrationAsync(differentTouristUser.Id, registration.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.Unauthorized, result.Error);
    }

    [Fact]
    public async Task PayRegistration_PastDeadline_ReturnsRegistrationDeadlinePassed()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, registration) = await SeedRegistrationAsync(context, daysUntilDeadline: -1);

        var result = await service.PayForRegistrationAsync(tourist.Id, registration.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.RegistrationDeadlinePassed, result.Error);
    }

    [Fact]
    public async Task PaymentsController_PayProposal_ReturnsOkOnSuccess()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, proposal) = await SeedAcceptedProposalAsync(context);

        var controller = new PaymentsController(service);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tourist.Id.ToString()),
            new Claim(ClaimTypes.Role, "Tourist")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var actionResult = await controller.PayForProposal(proposal.Id);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var paymentResult = Assert.IsType<PaymentResultDto>(okResult.Value);
        Assert.True(paymentResult.Success);
    }

    [Fact]
    public async Task PaymentsController_PayProposal_MapsFailedPaymentTo402PaymentRequired()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, proposal) = await SeedAcceptedProposalAsync(context);

        var controller = new PaymentsController(service);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tourist.Id.ToString()),
            new Claim(ClaimTypes.Role, "Tourist")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var actionResult = await controller.PayForProposal(
            proposal.Id,
            new PayProposalRequestDto { PaymentMethod = "FAIL" });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status402PaymentRequired, objectResult.StatusCode);
        var paymentResult = Assert.IsType<PaymentResultDto>(objectResult.Value);
        Assert.False(paymentResult.Success);
        Assert.Equal(PaymentError.PaymentFailed, paymentResult.Error);
    }

    [Fact]
    public async Task PaymentsController_PayProposal_MapsAlreadyPaidTo409Conflict()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, proposal) = await SeedAcceptedProposalAsync(context);

        var controller = new PaymentsController(service);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tourist.Id.ToString()),
            new Claim(ClaimTypes.Role, "Tourist")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        // First succeeds
        await controller.PayForProposal(proposal.Id);

        // Second conflicts
        var actionResult = await controller.PayForProposal(proposal.Id);
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task PaymentsController_PayProposal_UnauthenticatedUser_Returns401Unauthorized()
    {
        var (service, _, _) = CreatePaymentService();
        var controller = new PaymentsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } // No NameIdentifier claim
            }
        };

        var actionResult = await controller.PayForProposal(1);
        Assert.IsType<UnauthorizedObjectResult>(actionResult);
    }

    [Fact]
    public async Task PaymentsController_PayRegistration_ReturnsOkOnSuccess()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, registration) = await SeedRegistrationAsync(context);

        var controller = new PaymentsController(service);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tourist.Id.ToString()),
            new Claim(ClaimTypes.Role, "Tourist")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var actionResult = await controller.PayForRegistration(registration.Id);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var paymentResult = Assert.IsType<PaymentResultDto>(okResult.Value);
        Assert.True(paymentResult.Success);
    }

    [Fact]
    public async Task PaymentsController_PayRegistration_CapacityExceeded_Returns409Conflict()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, registration) = await SeedRegistrationAsync(
            context,
            maxParticipants: 1,
            existingConfirmed: 1);

        var controller = new PaymentsController(service);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tourist.Id.ToString()),
            new Claim(ClaimTypes.Role, "Tourist")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var actionResult = await controller.PayForRegistration(registration.Id);
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public void RegistrationModel_DefaultsStatusToPendingPayment()
    {
        var registration = new Registration();
        Assert.Equal(RegistrationStatus.PendingPayment, registration.Status);
    }

    [Fact]
    public async Task ApplicationDbContext_ModelConfiguration_HasPendingPaymentDefaultAndUniqueIndexes()
    {
        var (service, context, _) = CreatePaymentService();
        var model = context.Model;

        var regEntity = model.FindEntityType(typeof(Registration));
        Assert.NotNull(regEntity);
        var statusProp = regEntity.FindProperty("Status");
        Assert.NotNull(statusProp);
        Assert.Equal(RegistrationStatus.PendingPayment, statusProp.GetDefaultValue());

        var paymentEntity = model.FindEntityType(typeof(Payment));
        Assert.NotNull(paymentEntity);

        // The filter must cover the escrow-bearing states a duplicate deposit can occupy
        // under the current lifecycle, and must exclude Pending and Failed.
        const string expectedFilter =
            "\"Status\" IN ('Held','Released','Refunded','PartiallyRefunded','Completed')";

        var indexes = paymentEntity.GetIndexes();

        var proposalUniqueIdx = indexes.FirstOrDefault(
            i => i.GetDatabaseName() == "IX_Payments_ProposalId_Escrow_Unique");
        Assert.NotNull(proposalUniqueIdx);
        Assert.True(proposalUniqueIdx.IsUnique);
        Assert.NotNull(proposalUniqueIdx.GetFilter());
        Assert.Contains(expectedFilter, proposalUniqueIdx.GetFilter());
        Assert.Contains("\"ProposalId\" IS NOT NULL", proposalUniqueIdx.GetFilter());
        Assert.DoesNotContain("'Completed' AND", proposalUniqueIdx.GetFilter());

        var regUniqueIdx = indexes.FirstOrDefault(
            i => i.GetDatabaseName() == "IX_Payments_RegistrationId_Escrow_Unique");
        Assert.NotNull(regUniqueIdx);
        Assert.True(regUniqueIdx.IsUnique);
        Assert.NotNull(regUniqueIdx.GetFilter());
        Assert.Contains(expectedFilter, regUniqueIdx.GetFilter());
        Assert.Contains("\"RegistrationId\" IS NOT NULL", regUniqueIdx.GetFilter());

        // The stale Completed-only indexes must no longer exist in the model.
        Assert.DoesNotContain(indexes, i => i.GetDatabaseName() == "IX_Payments_ProposalId_Completed_Unique");
        Assert.DoesNotContain(indexes, i => i.GetDatabaseName() == "IX_Payments_RegistrationId_Completed_Unique");
    }

    [Fact]
    public void ApplicationDbContext_MilestoneModelConfiguration_HasUniqueOrderingPerPayment()
    {
        var (_, context, _) = CreatePaymentService();

        var milestoneEntity = context.Model.FindEntityType(typeof(PaymentMilestone));
        Assert.NotNull(milestoneEntity);

        var statusProp = milestoneEntity.FindProperty("Status");
        Assert.NotNull(statusProp);
        Assert.Equal(typeof(string), statusProp.GetProviderClrType());

        var amountProp = milestoneEntity.FindProperty("Amount");
        Assert.NotNull(amountProp);
        Assert.Equal(18, amountProp.GetPrecision());
        Assert.Equal(2, amountProp.GetScale());

        var uniqueIdx = milestoneEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "IX_PaymentMilestones_PaymentId_Index_Unique");
        Assert.NotNull(uniqueIdx);
        Assert.True(uniqueIdx.IsUnique);
        Assert.Equal(new[] { "PaymentId", "Index" },
            uniqueIdx.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task PayProposal_WhenUniqueConstraintViolated_ReturnsTypedAlreadyPaidConflict()
    {
        var (service, context, _) = CreatePaymentService();
        var (tourist, _, _, proposal) = await SeedAcceptedProposalAsync(context);

        // Pre-insert a completed payment directly in db to simulate concurrent completion bypassing app check
        var concurrentPayment = new Payment
        {
            TouristUserId = tourist.Id,
            TargetType = PaymentTargetType.Proposal,
            ProposalId = proposal.Id,
            Amount = proposal.ProposedPrice,
            Currency = "USD",
            Status = PaymentStatus.Completed,
            TransactionReference = "CONCURRENT-TXN",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        context.Payments.Add(concurrentPayment);
        await context.SaveChangesAsync();

        var result = await service.PayForProposalAsync(tourist.Id, proposal.Id);

        Assert.False(result.Success);
        Assert.Equal(PaymentError.AlreadyPaid, result.Error);
    }
}
