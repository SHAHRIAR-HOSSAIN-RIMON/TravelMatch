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

    public async Task<RegistrationResultDto> RegisterAsync(int touristId, int organizedTripId, CreateRegistrationDto request)
    {
        if (request.NumberOfTravelers < 1)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.InvalidNumberOfTravelers,
                Message = "Number of travelers must be at least 1."
            };
        }

        var touristExists = await _context.Users.AnyAsync(u =>
            u.Id == touristId &&
            u.Role == UserRole.Tourist &&
            u.IsActive);

        if (!touristExists)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.Unauthorized,
                Message = "Only active tourist accounts can register for trips."
            };
        }

        var organizedTrip = await _context.OrganizedTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == organizedTripId);

        if (organizedTrip == null)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.OrganizedTripNotFound,
                Message = "Organized trip not found."
            };
        }

        if (organizedTrip.Status != OrganizedTripStatus.RegistrationOpen)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.TripNotOpenForRegistration,
                Message = "This trip is not open for registration."
            };
        }

        if (organizedTrip.Status == OrganizedTripStatus.Cancelled)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.TripCancelled,
                Message = "This trip has been cancelled."
            };
        }

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (organizedTrip.RegistrationDeadline < todayUtc)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.DeadlinePassed,
                Message = "Registration deadline has passed."
            };
        }

        var existingRegistration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.OrganizedTripId == organizedTripId && r.TouristId == touristId);

        if (existingRegistration != null)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.DuplicateRegistration,
                Message = "You have already registered for this trip."
            };
        }

        var confirmedRegistrations = await _context.Registrations
            .Where(r => r.OrganizedTripId == organizedTripId && r.Status == RegistrationStatus.Confirmed)
            .SumAsync(r => r.NumberOfTravelers);

        var remainingSpots = organizedTrip.MaxParticipants - confirmedRegistrations;

        if (request.NumberOfTravelers > remainingSpots)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.ExceedsCapacity,
                Message = $"Number of travelers exceeds remaining spots. Only {remainingSpots} spot(s) available."
            };
        }

        if (remainingSpots <= 0)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.TripFull,
                Message = "This trip is full."
            };
        }

        var totalPrice = organizedTrip.PricePerPerson * request.NumberOfTravelers;

        var registration = new Registration
        {
            OrganizedTripId = organizedTripId,
            TouristId = touristId,
            NumberOfTravelers = request.NumberOfTravelers,
            Status = RegistrationStatus.Pending,
            TotalPrice = totalPrice,
            RegisteredAt = DateTime.UtcNow
        };

        _context.Registrations.Add(registration);

        await _context.SaveChangesAsync();

        return new RegistrationResultDto
        {
            Success = true,
            Message = "Registration created successfully. Please complete payment to confirm your spot.",
            Data = ToResponseDto(registration, organizedTrip)
        };
    }

    public async Task<RegistrationResultDto> ConfirmPaymentAsync(int touristId, int registrationId)
    {
        var registration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.TouristId == touristId);

        if (registration == null)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.RegistrationNotFound,
                Message = "Registration not found."
            };
        }

        if (registration.Status == RegistrationStatus.Confirmed)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.DuplicateRegistration,
                Message = "Registration is already confirmed."
            };
        }

        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.RegistrationNotFound,
                Message = "Registration has been cancelled."
            };
        }

        var organizedTrip = await _context.OrganizedTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == registration.OrganizedTripId);

        if (organizedTrip == null)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.OrganizedTripNotFound,
                Message = "Organized trip not found."
            };
        }

        if (organizedTrip.Status != OrganizedTripStatus.RegistrationOpen)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.TripNotOpenForRegistration,
                Message = "This trip is no longer open for registration."
            };
        }

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (organizedTrip.RegistrationDeadline < todayUtc)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.DeadlinePassed,
                Message = "Registration deadline has passed."
            };
        }

        var confirmedCount = await _context.Registrations
            .Where(r => r.OrganizedTripId == registration.OrganizedTripId && r.Status == RegistrationStatus.Confirmed)
            .SumAsync(r => r.NumberOfTravelers);

        var remainingSpots = organizedTrip.MaxParticipants - confirmedCount;

        if (registration.NumberOfTravelers > remainingSpots)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.ExceedsCapacity,
                Message = $"Not enough spots available. Only {remainingSpots} spot(s) remaining."
            };
        }

        registration.Status = RegistrationStatus.Confirmed;
        registration.ConfirmedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new RegistrationResultDto
        {
            Success = true,
            Message = "Payment confirmed. Your registration is now confirmed.",
            Data = ToResponseDto(registration, organizedTrip)
        };
    }

    public async Task<RegistrationResultDto> CancelRegistrationAsync(int touristId, int registrationId, string? reason)
    {
        var registration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.TouristId == touristId);

        if (registration == null)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.RegistrationNotFound,
                Message = "Registration not found."
            };
        }

        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.RegistrationNotFound,
                Message = "Registration is already cancelled."
            };
        }

        var organizedTrip = await _context.OrganizedTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == registration.OrganizedTripId);

        if (organizedTrip == null)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.OrganizedTripNotFound,
                Message = "Organized trip not found."
            };
        }

        if (organizedTrip.Status == OrganizedTripStatus.InProgress || organizedTrip.Status == OrganizedTripStatus.Completed)
        {
            return new RegistrationResultDto
            {
                Success = false,
                Error = RegistrationError.TripNotOpenForRegistration,
                Message = "Cannot cancel registration for a trip that has started or completed."
            };
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