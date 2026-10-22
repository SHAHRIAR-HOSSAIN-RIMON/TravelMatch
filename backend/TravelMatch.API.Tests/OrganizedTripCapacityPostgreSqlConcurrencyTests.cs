using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.DTOs.Registrations;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// PostgreSQL concurrency and regression tests for organized-trip capacity enforcement.
/// Uses a real disposable PostgreSQL database with independent DbContext instances and
/// transactions to prove that SELECT ... FOR UPDATE row locks prevent overbooking under concurrency.
/// </summary>
public class OrganizedTripCapacityPostgreSqlConcurrencyTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_PG_CONNECTION_STRING")
        ?? "Host=/tmp;Port=5433;Database=travelmatch_capacity_test_db;Username=postgres;Password=";

    private static ApplicationDbContext CreatePostgreSqlContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class DelayedPaymentGateway : ISandboxPaymentGateway
    {
        private readonly int _delayMs;
        private readonly bool _succeed;

        public List<decimal> ChargedAmounts { get; } = new();

        public DelayedPaymentGateway(int delayMs = 150, bool succeed = true)
        {
            _delayMs = delayMs;
            _succeed = succeed;
        }

        public async Task<PaymentSimulationResult> ProcessPaymentAsync(
            decimal amount, string currency, string? paymentMethod = null)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs);

            ChargedAmounts.Add(amount);

            return new PaymentSimulationResult
            {
                IsSuccess = _succeed,
                TransactionReference = _succeed ? $"PG-TX-{Guid.NewGuid():N}" : $"PG-FAIL-{Guid.NewGuid():N}",
                FailureReason = _succeed ? null : "Simulated payment gateway failure."
            };
        }
    }

    private static PaymentService CreatePaymentService(ApplicationDbContext db, DelayedPaymentGateway? gateway = null)
    {
        var gw = gateway ?? new DelayedPaymentGateway(delayMs: 0);
        var logger = NullLogger<PaymentService>.Instance;
        var notificationService = new NotificationService(db, NullLogger<NotificationService>.Instance, new NoopNotificationSender());
        return new PaymentService(db, gw, logger, notificationService);
    }

    private static async Task<(OrganizerProfile organizer, OrganizedTrip trip)> SeedTripAsync(
        int maxParticipants = 5,
        decimal pricePerPerson = 100m,
        int deadlineDaysAhead = 10)
    {
        await using var ctx = CreatePostgreSqlContext();

        var orgUser = new User
        {
            FullName = "Test Organizer",
            Email = $"org_{Guid.NewGuid():N}@example.com",
            PhoneNumber = "01700000000",
            Role = UserRole.Organizer,
            IsActive = true
        };
        ctx.Users.Add(orgUser);
        await ctx.SaveChangesAsync();

        var orgProfile = new OrganizerProfile
        {
            UserId = orgUser.Id,
            GroupOrganizationName = "Wilderness Adventures"
        };
        ctx.OrganizerProfiles.Add(orgProfile);
        await ctx.SaveChangesAsync();

        var trip = new OrganizedTrip
        {
            OrganizerId = orgProfile.Id,
            Title = "Highland Trek",
            Destination = "Sajek Valley",
            Description = "Adventurous mountain trip",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)),
            MaxParticipants = maxParticipants,
            PricePerPerson = pricePerPerson,
            Inclusions = "Food, Lodging",
            RegistrationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(deadlineDaysAhead)),
            Status = OrganizedTripStatus.RegistrationOpen,
            CreatedAt = DateTime.UtcNow
        };
        ctx.OrganizedTrips.Add(trip);
        await ctx.SaveChangesAsync();

        return (orgProfile, trip);
    }

    private static async Task<(User user, TouristProfile profile)> CreateTouristAsync()
    {
        await using var ctx = CreatePostgreSqlContext();

        var user = new User
        {
            FullName = $"Tourist {Guid.NewGuid():N}",
            Email = $"tourist_{Guid.NewGuid():N}@example.com",
            PhoneNumber = "01800000000",
            Role = UserRole.Tourist,
            IsActive = true
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var profile = new TouristProfile
        {
            UserId = user.Id
        };
        ctx.TouristProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        return (user, profile);
    }

    private static async Task<int> GetOccupiedSpotsAsync(int organizedTripId)
    {
        await using var ctx = CreatePostgreSqlContext();
        return await ctx.Registrations
            .Where(r => r.OrganizedTripId == organizedTripId &&
                        (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.PendingPayment))
            .SumAsync(r => r.NumberOfTravelers);
    }

    // ── 1. Registration creation racing with another registration ───────────────

    [Fact]
    public async Task Scenario1_ConcurrentRegistrations_CannotExceedMaxParticipants()
    {
        // Trip with MaxParticipants = 3
        var (_, trip) = await SeedTripAsync(maxParticipants: 3);

        var (touristA, _) = await CreateTouristAsync();
        var (touristB, _) = await CreateTouristAsync();

        // Both tourists attempt to register for 2 spots concurrently (total 4 > max 3)
        var barrier = new TaskCompletionSource();

        var taskA = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var svc = new RegistrationService(db);
            await barrier.Task;
            return await svc.RegisterAsync(touristA.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
        });

        var taskB = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var svc = new RegistrationService(db);
            await barrier.Task;
            return await svc.RegisterAsync(touristB.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
        });

        barrier.SetResult();
        var results = await Task.WhenAll(taskA, taskB);

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var failedResult = results.First(r => !r.Success);
        Assert.True(failedResult.Error is RegistrationError.ExceedsCapacity or RegistrationError.TripFull);

        var occupied = await GetOccupiedSpotsAsync(trip.Id);
        Assert.Equal(2, occupied);
        Assert.True(occupied <= trip.MaxParticipants);
    }

    // ── 2. Registration creation racing with payment confirmation ──────────────

    [Fact]
    public async Task Scenario2_RegistrationRacingWithPaymentConfirmation_MaintainsCapacityInvariant()
    {
        // Trip has MaxParticipants = 4
        var (_, trip) = await SeedTripAsync(maxParticipants: 4);

        // Tourist A already has a registration with 2 spots (PendingPayment)
        var (touristA, _) = await CreateTouristAsync();
        RegistrationResultDto regA;
        await using (var db = CreatePostgreSqlContext())
        {
            var svc = new RegistrationService(db);
            regA = await svc.RegisterAsync(touristA.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
        }
        Assert.True(regA.Success);

        // Remaining spots = 4 - 2 = 2 spots.
        // Tourist B tries to register for 2 spots while Tourist A is paying for their 2 spots.
        // Tourist C tries to register for 2 spots at the same time.
        // Total spots attempted = 2 (A confirmed) + 2 (B) + 2 (C) = 6 > 4.
        var (touristB, _) = await CreateTouristAsync();
        var (touristC, _) = await CreateTouristAsync();

        var barrier = new TaskCompletionSource();

        var payTaskA = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var gw = new DelayedPaymentGateway(delayMs: 100);
            var paySvc = CreatePaymentService(db, gw);
            await barrier.Task;
            return (object)await paySvc.PayForRegistrationAsync(touristA.Id, regA.Data!.Id);
        });

        var regTaskB = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var regSvc = new RegistrationService(db);
            await barrier.Task;
            return (object)await regSvc.RegisterAsync(touristB.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
        });

        var regTaskC = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var regSvc = new RegistrationService(db);
            await barrier.Task;
            return (object)await regSvc.RegisterAsync(touristC.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
        });

        barrier.SetResult();
        await Task.WhenAll(payTaskA, regTaskB, regTaskC);

        var occupied = await GetOccupiedSpotsAsync(trip.Id);
        Assert.Equal(4, occupied);
        Assert.True(occupied <= trip.MaxParticipants);
    }

    // ── 3. Two simultaneous payment confirmations for competing registrations ──

    [Fact]
    public async Task Scenario3_TwoSimultaneousPaymentConfirmations_CompeteForLastSpots_NoOverbooking()
    {
        // Trip with MaxParticipants = 3
        var (_, trip) = await SeedTripAsync(maxParticipants: 3);

        // Create 2 registrations with 2 travelers each (2 + 2 = 4 > 3).
        // Seed both registrations as PendingPayment directly in DB.
        var (touristA, profileA) = await CreateTouristAsync();
        var (touristB, profileB) = await CreateTouristAsync();

        int regAId, regBId;
        await using (var ctx = CreatePostgreSqlContext())
        {
            var rA = new Registration
            {
                OrganizedTripId = trip.Id,
                TouristId = profileA.Id,
                NumberOfTravelers = 2,
                Status = RegistrationStatus.PendingPayment,
                TotalPrice = trip.PricePerPerson * 2,
                RegisteredAt = DateTime.UtcNow
            };
            var rB = new Registration
            {
                OrganizedTripId = trip.Id,
                TouristId = profileB.Id,
                NumberOfTravelers = 2,
                Status = RegistrationStatus.PendingPayment,
                TotalPrice = trip.PricePerPerson * 2,
                RegisteredAt = DateTime.UtcNow
            };
            ctx.Registrations.AddRange(rA, rB);
            await ctx.SaveChangesAsync();
            regAId = rA.Id;
            regBId = rB.Id;
        }

        var barrier = new TaskCompletionSource();

        var taskA = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var gw = new DelayedPaymentGateway(delayMs: 80);
            var svc = CreatePaymentService(db, gw);
            await barrier.Task;
            return await svc.PayForRegistrationAsync(touristA.Id, regAId);
        });

        var taskB = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var gw = new DelayedPaymentGateway(delayMs: 80);
            var svc = CreatePaymentService(db, gw);
            await barrier.Task;
            return await svc.PayForRegistrationAsync(touristB.Id, regBId);
        });

        barrier.SetResult();
        var results = await Task.WhenAll(taskA, taskB);

        // At most one can successfully confirm payment
        var confirmedPayments = results.Count(r => r.Success);
        Assert.True(confirmedPayments <= 1, "At most one competing registration could confirm payment.");

        await using (var ctx = CreatePostgreSqlContext())
        {
            var confirmedTravelers = await ctx.Registrations
                .Where(r => r.OrganizedTripId == trip.Id && r.Status == RegistrationStatus.Confirmed)
                .SumAsync(r => r.NumberOfTravelers);

            Assert.True(confirmedTravelers <= trip.MaxParticipants,
                $"Confirmed travelers ({confirmedTravelers}) exceeded MaxParticipants ({trip.MaxParticipants}).");
        }
    }

    // ── 4. Manual confirmation racing with payment confirmation ────────────────

    [Fact]
    public async Task Scenario4_ManualConfirmationRacingWithPaymentConfirmation_SingleRegistration()
    {
        var (_, trip) = await SeedTripAsync(maxParticipants: 5);
        var (tourist, _) = await CreateTouristAsync();

        RegistrationResultDto regResult;
        await using (var db = CreatePostgreSqlContext())
        {
            var regSvc = new RegistrationService(db);
            regResult = await regSvc.RegisterAsync(tourist.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
        }
        Assert.True(regResult.Success);
        var regId = regResult.Data!.Id;

        var barrier = new TaskCompletionSource();
        var gw = new DelayedPaymentGateway(delayMs: 100);

        var paymentTask = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var paySvc = CreatePaymentService(db, gw);
            await barrier.Task;
            return await paySvc.PayForRegistrationAsync(tourist.Id, regId);
        });

        var manualTask = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var manualSvc = new RegistrationService(db);
            await barrier.Task;
            return await manualSvc.ConfirmPaymentAsync(tourist.Id, regId);
        });

        barrier.SetResult();
        var payResult = await paymentTask;
        var manualResult = await manualTask;

        // Exactly one should succeed, the other should be rejected as AlreadyPaid or DuplicateRegistration
        Assert.True(payResult.Success ^ manualResult.Success,
            $"Expected exactly one success: payResult={payResult.Success}, manualResult={manualResult.Success}");

        if (manualResult.Success)
        {
            Assert.False(payResult.Success);
            Assert.Equal(PaymentError.AlreadyPaid, payResult.Error);
            Assert.Empty(gw.ChargedAmounts); // Gateway was never charged!
        }
        else
        {
            Assert.True(payResult.Success);
            Assert.False(manualResult.Success);
            Assert.Equal(RegistrationError.DuplicateRegistration, manualResult.Error);
            Assert.Single(gw.ChargedAmounts);
        }

        // Final registration status is Confirmed exactly once
        await using (var ctx = CreatePostgreSqlContext())
        {
            var reg = await ctx.Registrations.FindAsync(regId);
            Assert.NotNull(reg);
            Assert.Equal(RegistrationStatus.Confirmed, reg.Status);
        }
    }

    // ── 5. A trip that becomes full while an operation waits for the lock ───────

    [Fact]
    public async Task Scenario5_TripBecomesFullWhileWaitingForLock_SecondOperationRejected()
    {
        // MaxParticipants = 2
        var (_, trip) = await SeedTripAsync(maxParticipants: 2);
        var (touristA, _) = await CreateTouristAsync();
        var (touristB, _) = await CreateTouristAsync();

        var barrier = new TaskCompletionSource();

        // Operation A takes all 2 spots
        var taskA = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var svc = new RegistrationService(db);
            await barrier.Task;
            return await svc.RegisterAsync(touristA.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
        });

        // Operation B also attempts to take 2 spots and must wait for A's lock
        var taskB = Task.Run(async () =>
        {
            await using var db = CreatePostgreSqlContext();
            var svc = new RegistrationService(db);
            await barrier.Task;
            return await svc.RegisterAsync(touristB.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
        });

        barrier.SetResult();
        var results = await Task.WhenAll(taskA, taskB);

        var successes = results.Count(r => r.Success);
        var failures = results.Count(r => !r.Success);

        Assert.Equal(1, successes);
        Assert.Equal(1, failures);

        var occupied = await GetOccupiedSpotsAsync(trip.Id);
        Assert.Equal(2, occupied);
    }

    // ── 6. Exact-capacity success ──────────────────────────────────────────────

    [Fact]
    public async Task Scenario6_ExactCapacityRegistration_Succeeds()
    {
        var (_, trip) = await SeedTripAsync(maxParticipants: 5);

        // Pre-fill 3 spots
        var (tourist1, _) = await CreateTouristAsync();
        await using (var db1 = CreatePostgreSqlContext())
        {
            var svc1 = new RegistrationService(db1);
            var res1 = await svc1.RegisterAsync(tourist1.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 3 });
            Assert.True(res1.Success);
        }

        // Exactly 2 remaining spots; register for exactly 2
        var (tourist2, _) = await CreateTouristAsync();
        await using (var db2 = CreatePostgreSqlContext())
        {
            var svc2 = new RegistrationService(db2);
            var res2 = await svc2.RegisterAsync(tourist2.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
            Assert.True(res2.Success);
        }

        var occupied = await GetOccupiedSpotsAsync(trip.Id);
        Assert.Equal(5, occupied);
    }

    // ── 7. Capacity-exceeding rejection ────────────────────────────────────────

    [Fact]
    public async Task Scenario7_CapacityExceedingRegistration_Rejected()
    {
        var (_, trip) = await SeedTripAsync(maxParticipants: 5);

        // Pre-fill 4 spots
        var (tourist1, _) = await CreateTouristAsync();
        await using (var db1 = CreatePostgreSqlContext())
        {
            var svc1 = new RegistrationService(db1);
            var res1 = await svc1.RegisterAsync(tourist1.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 4 });
            Assert.True(res1.Success);
        }

        // 1 remaining; try to register for 2
        var (tourist2, _) = await CreateTouristAsync();
        await using (var db2 = CreatePostgreSqlContext())
        {
            var svc2 = new RegistrationService(db2);
            var res2 = await svc2.RegisterAsync(tourist2.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });

            Assert.False(res2.Success);
            Assert.Equal(RegistrationError.ExceedsCapacity, res2.Error);
        }

        var occupied = await GetOccupiedSpotsAsync(trip.Id);
        Assert.Equal(4, occupied);
    }

    // ── 8. Cancelled registrations do not consume capacity ─────────────────────

    [Fact]
    public async Task Scenario8_CancelledRegistration_FreesCapacity()
    {
        var (_, trip) = await SeedTripAsync(maxParticipants: 3);

        // Fill all 3 spots
        var (tourist1, _) = await CreateTouristAsync();
        RegistrationResultDto res1;
        await using (var db1 = CreatePostgreSqlContext())
        {
            var svc1 = new RegistrationService(db1);
            res1 = await svc1.RegisterAsync(tourist1.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 3 });
            Assert.True(res1.Success);
        }

        // Tourist 2 tries to register -> fails (trip full)
        var (tourist2, _) = await CreateTouristAsync();
        await using (var db2 = CreatePostgreSqlContext())
        {
            var svc2 = new RegistrationService(db2);
            var res2 = await svc2.RegisterAsync(tourist2.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 3 });
            Assert.False(res2.Success);
            Assert.Equal(RegistrationError.TripFull, res2.Error);
        }

        // Tourist 1 cancels their registration
        await using (var db1 = CreatePostgreSqlContext())
        {
            var svc1 = new RegistrationService(db1);
            var cancelRes = await svc1.CancelRegistrationAsync(tourist1.Id, trip.Id, res1.Data!.Id, "Change of plans");
            Assert.True(cancelRes.Success);
        }

        // Now Tourist 2 registers for 3 spots -> succeeds
        await using (var db2 = CreatePostgreSqlContext())
        {
            var svc2 = new RegistrationService(db2);
            var res3 = await svc2.RegisterAsync(tourist2.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 3 });
            Assert.True(res3.Success);
        }

        var occupied = await GetOccupiedSpotsAsync(trip.Id);
        Assert.Equal(3, occupied);
    }

    // ── 9. Repeated payment attempts remaining idempotent ──────────────────────

    [Fact]
    public async Task Scenario9_RepeatedPaymentAttempts_RemainIdempotent()
    {
        var (_, trip) = await SeedTripAsync(maxParticipants: 5);
        var (tourist, _) = await CreateTouristAsync();

        RegistrationResultDto reg;
        await using (var db = CreatePostgreSqlContext())
        {
            var regSvc = new RegistrationService(db);
            reg = await regSvc.RegisterAsync(tourist.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
            Assert.True(reg.Success);
        }

        var gw = new DelayedPaymentGateway(delayMs: 0);

        // First payment attempt -> succeeds
        await using (var db = CreatePostgreSqlContext())
        {
            var paySvc = CreatePaymentService(db, gw);
            var pay1 = await paySvc.PayForRegistrationAsync(tourist.Id, reg.Data!.Id);
            Assert.True(pay1.Success);
            Assert.Single(gw.ChargedAmounts);
        }

        // Second payment attempt -> idempotent rejection with AlreadyPaid
        await using (var db = CreatePostgreSqlContext())
        {
            var paySvc = CreatePaymentService(db, gw);
            var pay2 = await paySvc.PayForRegistrationAsync(tourist.Id, reg.Data!.Id);
            Assert.False(pay2.Success);
            Assert.Equal(PaymentError.AlreadyPaid, pay2.Error);

            // Gateway was NOT charged a second time
            Assert.Single(gw.ChargedAmounts);
        }
    }

    // ── 10. Payment failure leaves registration and capacity state consistent ──

    [Fact]
    public async Task Scenario10_PaymentFailure_LeavesCapacityConsistentAndRetryable()
    {
        var (_, trip) = await SeedTripAsync(maxParticipants: 5);
        var (tourist, _) = await CreateTouristAsync();

        RegistrationResultDto reg;
        await using (var db = CreatePostgreSqlContext())
        {
            var regSvc = new RegistrationService(db);
            reg = await regSvc.RegisterAsync(tourist.Id, trip.Id, new CreateRegistrationDto { NumberOfTravelers = 2 });
            Assert.True(reg.Success);
        }

        // Payment gateway fails
        var failingGw = new DelayedPaymentGateway(delayMs: 0, succeed: false);
        await using (var db = CreatePostgreSqlContext())
        {
            var paySvc = CreatePaymentService(db, failingGw);
            var payFail = await paySvc.PayForRegistrationAsync(tourist.Id, reg.Data!.Id);
            Assert.False(payFail.Success);
            Assert.Equal(PaymentError.PaymentFailed, payFail.Error);
        }

        // Registration remains PendingPayment
        await using (var ctx = CreatePostgreSqlContext())
        {
            var dbReg = await ctx.Registrations.FindAsync(reg.Data!.Id);
            Assert.NotNull(dbReg);
            Assert.Equal(RegistrationStatus.PendingPayment, dbReg.Status);
        }

        // Capacity is still reserved (2 spots)
        var occupied = await GetOccupiedSpotsAsync(trip.Id);
        Assert.Equal(2, occupied);

        // Tourist can retry payment with a working gateway -> succeeds
        var succeedingGw = new DelayedPaymentGateway(delayMs: 0, succeed: true);
        await using (var db = CreatePostgreSqlContext())
        {
            var retryPaySvc = CreatePaymentService(db, succeedingGw);
            var retryPay = await retryPaySvc.PayForRegistrationAsync(tourist.Id, reg.Data!.Id);
            Assert.True(retryPay.Success);
        }

        // Registration is now Confirmed
        await using (var ctx = CreatePostgreSqlContext())
        {
            var dbReg = await ctx.Registrations.FindAsync(reg.Data!.Id);
            Assert.NotNull(dbReg);
            Assert.Equal(RegistrationStatus.Confirmed, dbReg.Status);
        }
    }
}
