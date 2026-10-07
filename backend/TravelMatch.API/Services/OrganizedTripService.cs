using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.OrganizedTrips;
using TravelMatch.API.DTOs.Registrations;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class OrganizedTripService : IOrganizedTripService
{
    private readonly ApplicationDbContext _context;

    public OrganizedTripService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrganizedTripResultDto> CreateAsync(int userId, CreateOrganizedTripDto request)
    {
        if (request is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.InvalidRequest,
                Message = "Request body is required."
            };
        }

        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var organizer = await _context.OrganizerProfiles
            .FirstOrDefaultAsync(op => op.UserId == userId);

        if (organizer is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.OrganizerNotFound,
                Message = "Authenticated user does not have an organizer profile."
            };
        }

        var organizedTrip = new OrganizedTrip
        {
            OrganizerId = organizer.Id,
            Title = request.Title.Trim(),
            Destination = request.Destination.Trim(),
            Description = request.Description.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaxParticipants = request.MaxParticipants,
            PricePerPerson = request.PricePerPerson,
            Inclusions = request.Inclusions.Trim(),
            Exclusions = string.IsNullOrWhiteSpace(request.Exclusions)
                ? null
                : request.Exclusions.Trim(),
            RegistrationDeadline = request.RegistrationDeadline,
            Status = OrganizedTripStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _context.OrganizedTrips.Add(organizedTrip);
        await _context.SaveChangesAsync();

        return new OrganizedTripResultDto
        {
            Success = true,
            Message = "Organized trip saved as draft.",
            Data = ToResponse(organizedTrip)
        };
    }

    public async Task<OrganizedTripResultDto> PublishAsync(int userId, int organizedTripId)
    {
        var organizer = await _context.OrganizerProfiles
            .FirstOrDefaultAsync(op => op.UserId == userId);

        if (organizer is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.OrganizerNotFound,
                Message = "Authenticated user does not have an organizer profile."
            };
        }

        var organizedTrip = await _context.OrganizedTrips
            .FirstOrDefaultAsync(t => t.Id == organizedTripId);

        if (organizedTrip is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.OrganizedTripNotFound,
                Message = "Organized trip was not found."
            };
        }

        if (organizedTrip.OrganizerId != organizer.Id)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.Unauthorized,
                Message = "You can only publish trips owned by your organizer profile."
            };
        }

        if (organizedTrip.Status != OrganizedTripStatus.Draft)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.InvalidStatusTransition,
                Message = "Only draft trips can be published."
            };
        }

        var createRequest = new CreateOrganizedTripDto
        {
            Title = organizedTrip.Title,
            Destination = organizedTrip.Destination,
            Description = organizedTrip.Description,
            StartDate = organizedTrip.StartDate,
            EndDate = organizedTrip.EndDate,
            MaxParticipants = organizedTrip.MaxParticipants,
            PricePerPerson = organizedTrip.PricePerPerson,
            Inclusions = organizedTrip.Inclusions,
            Exclusions = organizedTrip.Exclusions,
            RegistrationDeadline = organizedTrip.RegistrationDeadline
        };

        var validationError = ValidateCreateRequest(createRequest);
        if (validationError is not null)
        {
            return validationError;
        }

        organizedTrip.Status = OrganizedTripStatus.GuideSelection;
        organizedTrip.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new OrganizedTripResultDto
        {
            Success = true,
            Message = "Organized trip published and moved to GUIDE_SELECTION.",
            Data = ToResponse(organizedTrip)
        };
    }

    public async Task<OrganizedTripListResultDto> GetMyTripsAsync(int userId)
    {
        var organizer = await _context.OrganizerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(op => op.UserId == userId);

        if (organizer is null)
        {
            return new OrganizedTripListResultDto
            {
                Success = false,
                Error = OrganizedTripError.OrganizerNotFound,
                Message = "Authenticated user does not have an organizer profile."
            };
        }

        var trips = await _context.OrganizedTrips
            .AsNoTracking()
            .Where(t => t.OrganizerId == organizer.Id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var tripDtos = trips
            .Select(ToResponse)
            .ToList();

        if (tripDtos.Count == 0)
        {
            return new OrganizedTripListResultDto
            {
                Success = true,
                Error = OrganizedTripError.None,
                Message = "You do not have any organized trips yet.",
                Data = Array.Empty<OrganizedTripResponseDto>(),
                TotalCount = 0
            };
        }

        return new OrganizedTripListResultDto
        {
            Success = true,
            Error = OrganizedTripError.None,
            Message = $"Showing {tripDtos.Count} organized trip(s).",
            Data = tripDtos,
            TotalCount = tripDtos.Count
        };
    }

    public async Task<OrganizedTripResultDto> UpdateAsync(int userId, int organizedTripId, CreateOrganizedTripDto request)
    {
        if (request is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.InvalidRequest,
                Message = "Request body is required."
            };
        }

        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var organizer = await _context.OrganizerProfiles
            .FirstOrDefaultAsync(op => op.UserId == userId);

        if (organizer is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.OrganizerNotFound,
                Message = "Authenticated user does not have an organizer profile."
            };
        }

        var organizedTrip = await _context.OrganizedTrips
            .FirstOrDefaultAsync(t => t.Id == organizedTripId);

        if (organizedTrip is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.OrganizedTripNotFound,
                Message = "Organized trip was not found."
            };
        }

        if (organizedTrip.OrganizerId != organizer.Id)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.Unauthorized,
                Message = "You can only update trips owned by your organizer profile."
            };
        }

        if (organizedTrip.Status != OrganizedTripStatus.Draft && organizedTrip.Status != OrganizedTripStatus.GuideSelection)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.InvalidStatusTransition,
                Message = "Only draft and guide-selection trips can be edited."
            };
        }

        organizedTrip.Title = request.Title.Trim();
        organizedTrip.Destination = request.Destination.Trim();
        organizedTrip.Description = request.Description.Trim();
        organizedTrip.StartDate = request.StartDate;
        organizedTrip.EndDate = request.EndDate;
        organizedTrip.MaxParticipants = request.MaxParticipants;
        organizedTrip.PricePerPerson = request.PricePerPerson;
        organizedTrip.Inclusions = request.Inclusions.Trim();
        organizedTrip.Exclusions = string.IsNullOrWhiteSpace(request.Exclusions)
            ? null
            : request.Exclusions.Trim();
        organizedTrip.RegistrationDeadline = request.RegistrationDeadline;
        organizedTrip.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new OrganizedTripResultDto
        {
            Success = true,
            Message = "Organized trip updated successfully.",
            Data = ToResponse(organizedTrip)
        };
    }

    public async Task<OrganizedTripResultDto> CancelAsync(int userId, int organizedTripId)
    {
        var organizer = await _context.OrganizerProfiles
            .FirstOrDefaultAsync(op => op.UserId == userId);

        if (organizer is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.OrganizerNotFound,
                Message = "Authenticated user does not have an organizer profile."
            };
        }

        var organizedTrip = await _context.OrganizedTrips
            .FirstOrDefaultAsync(t => t.Id == organizedTripId);

        if (organizedTrip is null)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.OrganizedTripNotFound,
                Message = "Organized trip was not found."
            };
        }

        if (organizedTrip.OrganizerId != organizer.Id)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.Unauthorized,
                Message = "You can only cancel trips owned by your organizer profile."
            };
        }

        if (organizedTrip.Status == OrganizedTripStatus.Cancelled)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.InvalidStatusTransition,
                Message = "This organized trip is already cancelled."
            };
        }

        if (organizedTrip.Status == OrganizedTripStatus.InProgress || organizedTrip.Status == OrganizedTripStatus.Completed)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.InvalidStatusTransition,
                Message = "Trips that are already in progress or completed cannot be cancelled."
            };
        }

        if (organizedTrip.Status != OrganizedTripStatus.Draft &&
            organizedTrip.Status != OrganizedTripStatus.GuideSelection &&
            organizedTrip.Status != OrganizedTripStatus.RegistrationOpen &&
            organizedTrip.Status != OrganizedTripStatus.RegistrationClosed)
        {
            return new OrganizedTripResultDto
            {
                Success = false,
                Error = OrganizedTripError.InvalidStatusTransition,
                Message = "This trip cannot be cancelled in its current status."
            };
        }

        organizedTrip.Status = OrganizedTripStatus.Cancelled;
        organizedTrip.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new OrganizedTripResultDto
        {
            Success = true,
            Message = "Organized trip cancelled successfully.",
            Data = ToResponse(organizedTrip)
        };
    }

    public async Task<RegistrationListResultDto> GetRegistrationsAsync(int userId, int organizedTripId)
    {
        var organizer = await _context.OrganizerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(op => op.UserId == userId);

        if (organizer is null)
        {
            return RegistrationFailure(
                RegistrationError.OrganizerNotFound,
                "Authenticated user does not have an organizer profile.");
        }

        var organizedTrip = await _context.OrganizedTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == organizedTripId);

        if (organizedTrip is null)
        {
            return RegistrationFailure(
                RegistrationError.OrganizedTripNotFound,
                "Organized trip was not found.");
        }

        if (organizedTrip.OrganizerId != organizer.Id)
        {
            return RegistrationFailure(
                RegistrationError.Unauthorized,
                "You can only view registrations for trips owned by your organizer profile.");
        }

        var registrations = await (
            from registration in _context.Registrations.AsNoTracking()
            join touristProfile in _context.TouristProfiles.AsNoTracking()
                on registration.TouristId equals touristProfile.Id
            join tourist in _context.Users.AsNoTracking()
                on touristProfile.UserId equals tourist.Id
            where registration.OrganizedTripId == organizedTripId
            orderby registration.RegisteredAt descending
            select new RegistrationResponseDto
            {
                Id = registration.Id,
                OrganizedTripId = registration.OrganizedTripId,
                RegisteredAt = registration.RegisteredAt,
                Tourist = new RegistrationTouristDto
                {
                    Id = touristProfile.Id,
                    FullName = tourist.FullName,
                    Email = tourist.Email,
                    PhoneNumber = tourist.PhoneNumber
                }
            }).ToListAsync();

        return new RegistrationListResultDto
        {
            Success = true,
            Message = registrations.Count == 0
                ? "This organized trip has no registrations yet."
                : $"Showing {registrations.Count} registration(s).",
            Data = registrations,
            TotalCount = registrations.Count
        };
    }

    private static OrganizedTripResultDto? ValidateCreateRequest(CreateOrganizedTripDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return InvalidResult(OrganizedTripError.MissingRequiredField, "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Destination))
        {
            return InvalidResult(OrganizedTripError.MissingRequiredField, "Destination is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return InvalidResult(OrganizedTripError.MissingRequiredField, "Description is required.");
        }

        if (request.StartDate == default)
        {
            return InvalidResult(OrganizedTripError.MissingRequiredField, "Start date is required.");
        }

        if (request.EndDate == default)
        {
            return InvalidResult(OrganizedTripError.MissingRequiredField, "End date is required.");
        }

        if (request.RegistrationDeadline == default)
        {
            return InvalidResult(OrganizedTripError.MissingRequiredField, "Registration deadline is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Inclusions))
        {
            return InvalidResult(OrganizedTripError.MissingRequiredField, "Inclusions are required.");
        }

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.StartDate < todayUtc)
        {
            return InvalidResult(OrganizedTripError.StartDateInPast, "Start date cannot be in the past.");
        }

        if (request.EndDate <= request.StartDate)
        {
            return InvalidResult(OrganizedTripError.EndDateBeforeStartDate, "End date must be after start date.");
        }

        if (request.RegistrationDeadline >= request.StartDate)
        {
            return InvalidResult(OrganizedTripError.RegistrationDeadlineAfterStartDate, "Registration deadline must be before start date.");
        }

        if (request.MaxParticipants < 1)
        {
            return InvalidResult(OrganizedTripError.InvalidMaxParticipants, "Max participants must be at least 1.");
        }

        if (request.PricePerPerson <= 0)
        {
            return InvalidResult(OrganizedTripError.InvalidPricePerPerson, "Price per person must be greater than zero.");
        }

        return null;
    }

    private static OrganizedTripResultDto InvalidResult(OrganizedTripError error, string message)
    {
        return new OrganizedTripResultDto
        {
            Success = false,
            Error = error,
            Message = message
        };
    }

    private static RegistrationListResultDto RegistrationFailure(RegistrationError error, string message)
    {
        return new RegistrationListResultDto
        {
            Success = false,
            Error = error,
            Message = message
        };
    }

    private static OrganizedTripResponseDto ToResponse(OrganizedTrip organizedTrip)
    {
        return new OrganizedTripResponseDto
        {
            Id = organizedTrip.Id,
            OrganizerId = organizedTrip.OrganizerId,
            Title = organizedTrip.Title,
            Destination = organizedTrip.Destination,
            Description = organizedTrip.Description,
            StartDate = organizedTrip.StartDate,
            EndDate = organizedTrip.EndDate,
            MaxParticipants = organizedTrip.MaxParticipants,
            PricePerPerson = organizedTrip.PricePerPerson,
            Inclusions = organizedTrip.Inclusions,
            Exclusions = organizedTrip.Exclusions,
            RegistrationDeadline = organizedTrip.RegistrationDeadline,
            Status = organizedTrip.Status.ToString(),
            CreatedAt = organizedTrip.CreatedAt,
            UpdatedAt = organizedTrip.UpdatedAt
        };
    }
}
