using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Registrations;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class RegistrationService : IRegistrationService
{
    private readonly ApplicationDbContext _context;

    public RegistrationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RegistrationResultDto> RegisterAsync(
        int touristUserId,
        int organizedTripId,
        CreateRegistrationDto request)
    {
        if (request is null || request.NumberOfTravelers < 1)
        {
            return Failure(
                RegistrationError.InvalidNumberOfTravelers,
                "Number of travelers must be at least 1.");
        }

        var touristProfile = await ResolveActiveTouristProfileAsync(touristUserId);
        if (touristProfile is null)
        {
            return Failure(
                RegistrationError.Unauthorized,
                "Only active tourists with a tourist profile can register for trips.");
        }

        var tx = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        await using var _ = tx;

        OrganizedTrip? organizedTrip;
        if (_context.Database.IsRelational())
        {
            organizedTrip = await _context.OrganizedTrips
                .FromSqlInterpolated(
                    $"SELECT * FROM \"OrganizedTrips\" WHERE \"Id\" = {organizedTripId} FOR UPDATE")
                .FirstOrDefaultAsync();
        }
        else
        {
            organizedTrip = await _context.OrganizedTrips
                .FirstOrDefaultAsync(t => t.Id == organizedTripId);
        }

        if (organizedTrip is null)
        {
            return Failure(RegistrationError.OrganizedTripNotFound, "Organized trip not found.");
        }

        if (organizedTrip.Status == OrganizedTripStatus.Cancelled)
        {
            return Failure(RegistrationError.TripCancelled, "This trip has been cancelled.");
        }

        if (organizedTrip.Status != OrganizedTripStatus.RegistrationOpen)
        {
            return Failure(
                RegistrationError.TripNotOpenForRegistration,
                "This trip is not open for registration.");
        }

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (organizedTrip.RegistrationDeadline < todayUtc)
        {
            return Failure(RegistrationError.DeadlinePassed, "Registration deadline has passed.");
        }

        var existingRegistration = await _context.Registrations
            .FirstOrDefaultAsync(r =>
                r.OrganizedTripId == organizedTripId &&
                r.TouristId == touristProfile.Id);

        if (existingRegistration is not null)
        {
            if (existingRegistration.Status == RegistrationStatus.Cancelled)
            {
                return Failure(
                    RegistrationError.DuplicateRegistration,
                    "A previous registration for this trip already exists and cannot be duplicated.");
            }

            return Failure(
                RegistrationError.DuplicateRegistration,
                "You have already registered for this trip.");
        }

        var occupiedSpots = await OccupiedSpotCountAsync(organizedTripId);
        var remainingSpots = organizedTrip.MaxParticipants - occupiedSpots;

        if (remainingSpots <= 0)
        {
            return Failure(RegistrationError.TripFull, "This trip is full.");
        }

        if (request.NumberOfTravelers > remainingSpots)
        {
            return Failure(
                RegistrationError.ExceedsCapacity,
                $"Number of travelers exceeds remaining spots. Only {remainingSpots} spot(s) available.");
        }

        var registration = new Registration
        {
            OrganizedTripId = organizedTripId,
            TouristId = touristProfile.Id,
            NumberOfTravelers = request.NumberOfTravelers,
            Status = RegistrationStatus.PendingPayment,
            TotalPrice = organizedTrip.PricePerPerson * request.NumberOfTravelers,
            RegisteredAt = DateTime.UtcNow
        };

        _context.Registrations.Add(registration);

        try
        {
            await _context.SaveChangesAsync();
            if (tx is not null) await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Failure(
                RegistrationError.DuplicateRegistration,
                "You have already registered for this trip.");
        }

        return new RegistrationResultDto
        {
            Success = true,
            Message = "Registration created successfully. Please complete payment to confirm your spot.",
            Data = ToResponseDto(registration, organizedTrip)
        };
    }

    public async Task<RegistrationResultDto> ConfirmPaymentAsync(int touristUserId, int registrationId)
    {
        var touristProfile = await ResolveActiveTouristProfileAsync(touristUserId);
        if (touristProfile is null)
        {
            return Failure(
                RegistrationError.Unauthorized,
                "Only active tourists with a tourist profile can confirm a registration.");
        }

        var tx = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        await using var _ = tx;

        var registration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.TouristId == touristProfile.Id);

        if (registration is null)
        {
            return Failure(RegistrationError.RegistrationNotFound, "Registration not found.");
        }

        if (registration.Status == RegistrationStatus.Confirmed)
        {
            return Failure(RegistrationError.DuplicateRegistration, "Registration is already confirmed.");
        }

        if (registration.Status != RegistrationStatus.PendingPayment)
        {
            return Failure(
                RegistrationError.TripNotOpenForRegistration,
                "Only pending registrations can be confirmed.");
        }

        OrganizedTrip? organizedTrip;
        if (_context.Database.IsRelational())
        {
            organizedTrip = await _context.OrganizedTrips
                .FromSqlInterpolated(
                    $"SELECT * FROM \"OrganizedTrips\" WHERE \"Id\" = {registration.OrganizedTripId} FOR UPDATE")
                .FirstOrDefaultAsync();
        }
        else
        {
            organizedTrip = await _context.OrganizedTrips
                .FirstOrDefaultAsync(t => t.Id == registration.OrganizedTripId);
        }

        if (organizedTrip is null)
        {
            return Failure(RegistrationError.OrganizedTripNotFound, "Organized trip not found.");
        }

        if (_context.Database.IsRelational())
        {
            await _context.Entry(registration).ReloadAsync();
            if (_context.Entry(registration).State == EntityState.Detached)
            {
                return Failure(RegistrationError.RegistrationNotFound, "Registration not found.");
            }
        }

        if (registration.Status == RegistrationStatus.Confirmed)
        {
            return Failure(RegistrationError.DuplicateRegistration, "Registration is already confirmed.");
        }

        if (registration.Status != RegistrationStatus.PendingPayment)
        {
            return Failure(
                RegistrationError.TripNotOpenForRegistration,
                "Only pending registrations can be confirmed.");
        }

        if (organizedTrip.Status != OrganizedTripStatus.RegistrationOpen)
        {
            return Failure(
                RegistrationError.TripNotOpenForRegistration,
                "This trip is no longer open for registration.");
        }

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (organizedTrip.RegistrationDeadline < todayUtc)
        {
            return Failure(RegistrationError.DeadlinePassed, "Registration deadline has passed.");
        }

        var occupiedSpots = await OccupiedSpotCountAsync(organizedTrip.Id, excludeRegistrationId: registration.Id);
        var remainingSpots = organizedTrip.MaxParticipants - occupiedSpots;

        if (registration.NumberOfTravelers > remainingSpots)
        {
            return Failure(
                RegistrationError.ExceedsCapacity,
                $"Not enough spots available. Only {Math.Max(remainingSpots, 0)} spot(s) remaining.");
        }

        registration.Status = RegistrationStatus.Confirmed;
        registration.ConfirmedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();

        return new RegistrationResultDto
        {
            Success = true,
            Message = "Payment confirmed. Your registration is now confirmed.",
            Data = ToResponseDto(registration, organizedTrip)
        };
    }

    public async Task<RegistrationResultDto> CancelRegistrationAsync(
        int touristUserId,
        int organizedTripId,
        int registrationId,
        string? reason)
    {
        var touristProfile = await ResolveActiveTouristProfileAsync(touristUserId);
        if (touristProfile is null)
        {
            return Failure(
                RegistrationError.Unauthorized,
                "Only active tourists with a tourist profile can cancel a registration.");
        }

        var registration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.TouristId == touristProfile.Id);

        if (registration is null || registration.OrganizedTripId != organizedTripId)
        {
            return Failure(RegistrationError.RegistrationNotFound, "Registration not found.");
        }

        return await CancelCoreAsync(registration, reason);
    }

    public async Task<RegistrationResultDto> CancelAsOrganizerAsync(
        int organizerUserId,
        int organizedTripId,
        int registrationId,
        string? reason)
    {
        var organizer = await _context.OrganizerProfiles
            .FirstOrDefaultAsync(op => op.UserId == organizerUserId);

        if (organizer is null)
        {
            return Failure(
                RegistrationError.OrganizerNotFound,
                "Authenticated user does not have an organizer profile.");
        }

        var organizedTrip = await _context.OrganizedTrips
            .FirstOrDefaultAsync(t => t.Id == organizedTripId);

        if (organizedTrip is null)
        {
            return Failure(RegistrationError.OrganizedTripNotFound, "Organized trip was not found.");
        }

        if (organizedTrip.OrganizerId != organizer.Id)
        {
            return Failure(
                RegistrationError.Unauthorized,
                "You can only manage registrations for trips owned by your organizer profile.");
        }

        var registration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.OrganizedTripId == organizedTripId);

        if (registration is null)
        {
            return Failure(RegistrationError.RegistrationNotFound, "Registration not found.");
        }

        return await CancelCoreAsync(registration, reason, organizedTrip);
    }

    private async Task<RegistrationResultDto> CancelCoreAsync(
        Registration registration,
        string? reason,
        OrganizedTrip? organizedTrip = null)
    {
        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return Failure(
                RegistrationError.TripNotOpenForRegistration,
                "Registration is already cancelled.");
        }

        organizedTrip ??= await _context.OrganizedTrips
            .FirstOrDefaultAsync(t => t.Id == registration.OrganizedTripId);

        if (organizedTrip is null)
        {
            return Failure(RegistrationError.OrganizedTripNotFound, "Organized trip not found.");
        }

        if (organizedTrip.Status is OrganizedTripStatus.InProgress or OrganizedTripStatus.Completed)
        {
            return Failure(
                RegistrationError.TripNotOpenForRegistration,
                "Cannot cancel registration for a trip that has started or completed.");
        }

        registration.Status = RegistrationStatus.Cancelled;
        registration.CancelledAt = DateTime.UtcNow;
        registration.CancellationReason = reason;

        await _context.SaveChangesAsync();

        return new RegistrationResultDto
        {
            Success = true,
            Message = "Registration cancelled successfully.",
            Data = ToResponseDto(registration, organizedTrip)
        };
    }

    private async Task<TouristProfile?> ResolveActiveTouristProfileAsync(int touristUserId)
    {
        var touristExists = await _context.Users.AnyAsync(u =>
            u.Id == touristUserId &&
            u.Role == UserRole.Tourist &&
            u.IsActive);

        if (!touristExists)
        {
            return null;
        }

        return await _context.TouristProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == touristUserId);
    }

    private async Task<int> OccupiedSpotCountAsync(int organizedTripId, int? excludeRegistrationId = null)
    {
        var query = _context.Registrations.Where(r =>
            r.OrganizedTripId == organizedTripId &&
            (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.PendingPayment));

        if (excludeRegistrationId is not null)
        {
            query = query.Where(r => r.Id != excludeRegistrationId.Value);
        }

        return await query.SumAsync(r => r.NumberOfTravelers);
    }

    private static RegistrationResultDto Failure(RegistrationError error, string message)
    {
        return new RegistrationResultDto
        {
            Success = false,
            Error = error,
            Message = message
        };
    }

    private static RegistrationResponseDto ToResponseDto(Registration registration, OrganizedTrip organizedTrip)
    {
        return new RegistrationResponseDto
        {
            Id = registration.Id,
            OrganizedTripId = registration.OrganizedTripId,
            NumberOfTravelers = registration.NumberOfTravelers,
            Status = registration.Status.ToString(),
            TotalPrice = registration.TotalPrice,
            RegisteredAt = registration.RegisteredAt,
            ConfirmedAt = registration.ConfirmedAt,
            CancelledAt = registration.CancelledAt,
            CancellationReason = registration.CancellationReason,
            OrganizedTrip = new OrganizedTripSummaryDto
            {
                Id = organizedTrip.Id,
                Title = organizedTrip.Title,
                Destination = organizedTrip.Destination,
                StartDate = organizedTrip.StartDate,
                EndDate = organizedTrip.EndDate,
                PricePerPerson = organizedTrip.PricePerPerson,
                Status = organizedTrip.Status.ToString()
            }
        };
    }
}
