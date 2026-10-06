using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.OrganizedTrips;
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
