using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.GuideApplications;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class GuideApplicationService : IGuideApplicationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GuideApplicationService> _logger;

    public GuideApplicationService(
        ApplicationDbContext context,
        ILogger<GuideApplicationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OpenOrganizedTripListResultDto> GetAvailableTripsAsync(
        int guideUserId,
        int page = 1,
        int pageSize = 20)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        try
        {
            var verification = await GetGuideVerificationAsync(guideUserId);

            if (verification is null)
            {
                return new OpenOrganizedTripListResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "Guide profile not found."
                };
            }

            var query = _context.OrganizedTrips
                .AsNoTracking()
                .Where(t => t.Status == OrganizedTripStatus.ApplicationOpen);

            var totalCount = await query.CountAsync();

            var trips = await query
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new OpenOrganizedTripDto
                {
                    Id = t.Id,
                    OrganizerId = t.OrganizerId,
                    Title = t.Title,
                    Destination = t.Destination,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Description = t.Description,
                    Status = t.Status.ToString(),
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            return new OpenOrganizedTripListResultDto
            {
                Success = true,
                Error = GuideApplicationError.None,
                Message = totalCount == 0
                    ? "No organized trips accepting guide applications right now."
                    : $"Showing {trips.Count} of {totalCount} available trip(s).",
                Data = trips,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                GuideVerificationStatus = verification.Name,
                CanApply = verification.IsVerified
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load available organized trips for guide {GuideUserId}.",
                guideUserId);

            return new OpenOrganizedTripListResultDto
            {
                Success = false,
                Error = GuideApplicationError.ServerError,
                Message = "Couldn't load available trips."
            };
        }
    }

    public async Task<OpenOrganizedTripDetailResultDto> GetTripDetailAsync(
        int guideUserId,
        int tripId)
    {
        if (tripId <= 0)
        {
            return new OpenOrganizedTripDetailResultDto
            {
                Success = false,
                Error = GuideApplicationError.TripNotFound,
                Message = "Trip not found."
            };
        }

        try
        {
            var verification = await GetGuideVerificationAsync(guideUserId);

            if (verification is null)
            {
                return new OpenOrganizedTripDetailResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "Guide profile not found."
                };
            }

            var trip = await _context.OrganizedTrips
                .AsNoTracking()
                .Where(t => t.Id == tripId)
                .Select(t => new OpenOrganizedTripDto
                {
                    Id = t.Id,
                    OrganizerId = t.OrganizerId,
                    Title = t.Title,
                    Destination = t.Destination,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Description = t.Description,
                    Status = t.Status.ToString(),
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (trip is null)
            {
                return new OpenOrganizedTripDetailResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.TripNotFound,
                    Message = "Trip not found.",
                    GuideVerificationStatus = verification.Name,
                    CanApply = verification.IsVerified
                };
            }

            return new OpenOrganizedTripDetailResultDto
            {
                Success = true,
                Error = GuideApplicationError.None,
                Message = "Trip loaded successfully.",
                Data = trip,
                GuideVerificationStatus = verification.Name,
                CanApply = verification.IsVerified
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load trip {TripId} for guide {GuideUserId}.",
                tripId,
                guideUserId);

            return new OpenOrganizedTripDetailResultDto
            {
                Success = false,
                Error = GuideApplicationError.ServerError,
                Message = "Couldn't load trip details."
            };
        }
    }

    public async Task<GuideApplicationResultDto> ApplyAsync(
        int guideUserId,
        int tripId,
        CreateGuideApplicationDto request)
    {
        if (tripId <= 0)
        {
            return new GuideApplicationResultDto
            {
                Success = false,
                Error = GuideApplicationError.TripNotFound,
                Message = "Trip not found."
            };
        }

        request.Message = request.Message?.Trim() ?? string.Empty;

        if (request.ProposedPrice <= 0)
        {
            return new GuideApplicationResultDto
            {
                Success = false,
                Error = GuideApplicationError.InvalidPrice,
                Message = "Proposed price must be greater than 0."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new GuideApplicationResultDto
            {
                Success = false,
                Error = GuideApplicationError.MessageRequired,
                Message = "Message is required."
            };
        }

        try
        {
            var verification = await GetGuideVerificationAsync(guideUserId);

            if (verification is null)
            {
                return new GuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "Guide profile not found."
                };
            }

            if (!verification.IsVerified)
            {
                return new GuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.GuideNotVerified,
                    Message = "You must be verified to apply for guide positions."
                };
            }

            var trip = await _context.OrganizedTrips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip is null)
            {
                return new GuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.TripNotFound,
                    Message = "Trip not found."
                };
            }

            if (trip.Status != OrganizedTripStatus.ApplicationOpen)
            {
                return new GuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.TripNotAcceptingApplications,
                    Message = "This trip is no longer accepting guide applications."
                };
            }

            var existingApplication = await _context.GuideApplications
                .AsNoTracking()
                .AnyAsync(a =>
                    a.GuideId == guideUserId &&
                    a.TripId == tripId &&
                    a.Status != GuideApplicationStatus.Rejected);

            if (existingApplication)
            {
                return new GuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.AlreadyApplied,
                    Message = "You've already applied to this trip."
                };
            }

            var application = new GuideApplication
            {
                GuideId = guideUserId,
                TripId = tripId,
                ProposedPrice = request.ProposedPrice,
                Message = request.Message,
                Status = GuideApplicationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();

            _context.GuideApplications.Add(application);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new GuideApplicationResultDto
            {
                Success = true,
                Message = "Application submitted!",
                Data = new GuideApplicationResponseDto
                {
                    Id = application.Id,
                    GuideId = application.GuideId,
                    TripId = application.TripId,
                    ProposedPrice = application.ProposedPrice,
                    Message = application.Message,
                    Status = application.Status.ToString(),
                    CreatedAt = application.CreatedAt,
                    UpdatedAt = application.UpdatedAt
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to submit guide application for trip {TripId} by guide {GuideUserId}.",
                tripId,
                guideUserId);

            return new GuideApplicationResultDto
            {
                Success = false,
                Error = GuideApplicationError.ServerError,
                Message = "Failed to submit application. Please try again."
            };
        }
    }

    public async Task<GuideApplicationListResultDto> GetMyApplicationsAsync(
        int guideUserId,
        int page = 1,
        int pageSize = 20)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        try
        {
            var verification = await GetGuideVerificationAsync(guideUserId);

            if (verification is null)
            {
                return new GuideApplicationListResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "Guide profile not found."
                };
            }

            var query = _context.GuideApplications
                .AsNoTracking()
                .Where(a => a.GuideId == guideUserId);

            var totalCount = await query.CountAsync();

            var applications = await query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new GuideApplicationDto
                {
                    Id = a.Id,
                    GuideId = a.GuideId,
                    GuideName = a.GuideId == guideUserId
                        ? "Me"
                        : "Unknown",
                    TripId = a.TripId,
                    TripTitle = a.Trip.Title,
                    ProposedPrice = a.ProposedPrice,
                    Message = a.Message,
                    Status = a.Status.ToString(),
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .ToListAsync();

            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            return new GuideApplicationListResultDto
            {
                Success = true,
                Error = GuideApplicationError.None,
                Message = totalCount == 0
                    ? "You have no guide applications."
                    : $"Showing {applications.Count} of {totalCount} application(s).",
                Data = applications,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load guide applications for guide {GuideUserId}.",
                guideUserId);

            return new GuideApplicationListResultDto
            {
                Success = false,
                Error = GuideApplicationError.ServerError,
                Message = "Couldn't load your applications."
            };
        }
    }

    public async Task<GuideApplicationDetailResultDto> GetApplicationAsync(
        int guideUserId,
        int applicationId)
    {
        if (applicationId <= 0)
        {
            return new GuideApplicationDetailResultDto
            {
                Success = false,
                Error = GuideApplicationError.Unauthorized,
                Message = "Invalid application."
            };
        }

        try
        {
            var verification = await GetGuideVerificationAsync(guideUserId);

            if (verification is null)
            {
                return new GuideApplicationDetailResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "Guide profile not found."
                };
            }

            var application = await _context.GuideApplications
                .AsNoTracking()
                .Where(a => a.Id == applicationId && a.GuideId == guideUserId)
                .Select(a => new GuideApplicationDto
                {
                    Id = a.Id,
                    GuideId = a.GuideId,
                    GuideName = "Me",
                    TripId = a.TripId,
                    TripTitle = a.Trip.Title,
                    ProposedPrice = a.ProposedPrice,
                    Message = a.Message,
                    Status = a.Status.ToString(),
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (application is null)
            {
                return new GuideApplicationDetailResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "Application not found."
                };
            }

            return new GuideApplicationDetailResultDto
            {
                Success = true,
                Error = GuideApplicationError.None,
                Message = "Application loaded successfully.",
                Data = application
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load application {ApplicationId} for guide {GuideUserId}.",
                applicationId,
                guideUserId);

            return new GuideApplicationDetailResultDto
            {
                Success = false,
                Error = GuideApplicationError.ServerError,
                Message = "Couldn't load application."
            };
        }
    }

    public async Task<GuideApplicationListResultDto> GetTripApplicationsAsync(
        int organizerUserId,
        int tripId,
        int page = 1,
        int pageSize = 20)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        if (tripId <= 0)
        {
            return new GuideApplicationListResultDto
            {
                Success = false,
                Error = GuideApplicationError.TripNotFound,
                Message = "Trip not found."
            };
        }

        try
        {
            var organizerExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u =>
                    u.Id == organizerUserId &&
                    u.Role == UserRole.Organizer &&
                    u.IsActive);

            if (!organizerExists)
            {
                return new GuideApplicationListResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "Organizer not found."
                };
            }

            var trip = await _context.OrganizedTrips
                .AsNoTracking()
                .Where(t => t.Id == tripId && t.OrganizerId == organizerUserId)
                .Select(t => new { t.Id })
                .FirstOrDefaultAsync();

            if (trip is null)
            {
                return new GuideApplicationListResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "You are not authorized to view applications for this trip."
                };
            }

            var query = _context.GuideApplications
                .AsNoTracking()
                .Where(a => a.TripId == tripId);

            var totalCount = await query.CountAsync();

            var applications = await query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new GuideApplicationDto
                {
                    Id = a.Id,
                    GuideId = a.GuideId,
                    GuideName = a.Guide.FullName,
                    TripId = a.TripId,
                    TripTitle = a.Trip.Title,
                    ProposedPrice = a.ProposedPrice,
                    Message = a.Message,
                    Status = a.Status.ToString(),
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .ToListAsync();

            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            return new GuideApplicationListResultDto
            {
                Success = true,
                Error = GuideApplicationError.None,
                Message = totalCount == 0
                    ? "No applications received yet for this trip."
                    : $"Showing {applications.Count} of {totalCount} application(s).",
                Data = applications,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load applications for trip {TripId} by organizer {OrganizerUserId}.",
                tripId,
                organizerUserId);

            return new GuideApplicationListResultDto
            {
                Success = false,
                Error = GuideApplicationError.ServerError,
                Message = "Couldn't load applications."
            };
        }
    }

    public async Task<SelectGuideApplicationResultDto> SelectGuideApplicationAsync(
        int organizerUserId,
        int tripId,
        int applicationId)
    {
        if (organizerUserId <= 0)
        {
            return new SelectGuideApplicationResultDto
            {
                Success = false,
                Error = GuideApplicationError.Unauthorized,
                Message = "Organizer not found."
            };
        }

        if (tripId <= 0)
        {
            return new SelectGuideApplicationResultDto
            {
                Success = false,
                Error = GuideApplicationError.TripNotFound,
                Message = "Trip not found."
            };
        }

        if (applicationId <= 0)
        {
            return new SelectGuideApplicationResultDto
            {
                Success = false,
                Error = GuideApplicationError.ApplicationNotFound,
                Message = "Application not found."
            };
        }

        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var organizer = await _context.OrganizerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(op => op.UserId == organizerUserId);

            if (organizer is null)
            {
                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.OrganizerNotFound,
                    Message = "Authenticated user does not have an organizer profile."
                };
            }

            var trip = await _context.OrganizedTrips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip is null)
            {
                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.TripNotFound,
                    Message = "Organized trip was not found."
                };
            }

            if (trip.OrganizerId != organizerUserId)
            {
                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.Unauthorized,
                    Message = "You can only select a guide for trips owned by your organizer profile."
                };
            }

            if (trip.Status != OrganizedTripStatus.GuideSelection)
            {
                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.TripNotInGuideSelection,
                    Message = "Guide selection is only available while the trip is in GUIDE_SELECTION status."
                };
            }

            if (trip.SelectedGuideId.HasValue)
            {
                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.GuideAlreadySelected,
                    Message = "A guide has already been selected for this trip."
                };
            }

            var application = await _context.GuideApplications
                .Include(a => a.Guide)
                .FirstOrDefaultAsync(a =>
                    a.Id == applicationId &&
                    a.TripId == tripId);

            if (application is null)
            {
                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.ApplicationNotFound,
                    Message = "Application not found for this trip."
                };
            }

            if (application.Status != GuideApplicationStatus.Pending)
            {
                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.ApplicationNotPending,
                    Message = "Only pending applications can be selected."
                };
            }

            var guideProfile = await _context.GuideProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.UserId == application.GuideId);

            if (guideProfile is null ||
                guideProfile.VerificationStatus != VerificationStatus.Verified)
            {
                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = GuideApplicationError.GuideNotVerified,
                    Message = "The selected guide is not verified and cannot be assigned."
                };
            }

            var now = DateTime.UtcNow;

            var acceptedRows = await _context.OrganizedTrips
                .Where(t =>
                    t.Id == tripId &&
                    t.OrganizerId == organizer.Id &&
                    t.Status == OrganizedTripStatus.GuideSelection &&
                    !t.SelectedGuideId.HasValue)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.SelectedGuideId, application.GuideId)
                    .SetProperty(t => t.SelectedGuideApplicationId, application.Id)
                    .SetProperty(t => t.Status, OrganizedTripStatus.RegistrationOpen)
                    .SetProperty(t => t.UpdatedAt, now));

            if (acceptedRows == 0)
            {
                var currentTrip = await _context.OrganizedTrips
                    .AsNoTracking()
                    .Where(t => t.Id == tripId)
                    .Select(t => new
                    {
                        t.Status,
                        t.SelectedGuideId
                    })
                    .FirstOrDefaultAsync();

                var alreadySelected = currentTrip != null && currentTrip.SelectedGuideId.HasValue;
                var wrongStatus = currentTrip != null && currentTrip.Status != OrganizedTripStatus.GuideSelection;

                return new SelectGuideApplicationResultDto
                {
                    Success = false,
                    Error = alreadySelected
                        ? GuideApplicationError.GuideAlreadySelected
                        : wrongStatus
                            ? GuideApplicationError.TripNotInGuideSelection
                            : GuideApplicationError.TripNotInGuideSelection,
                    Message = alreadySelected
                        ? "A guide has already been selected for this trip."
                        : "Guide selection is not available for this trip in its current state."
                };
            }

            application.Status = GuideApplicationStatus.Accepted;
            application.UpdatedAt = now;

            await _context.GuideApplications
                .Where(a =>
                    a.TripId == tripId &&
                    a.Id != applicationId &&
                    a.Status == GuideApplicationStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(a => a.Status, GuideApplicationStatus.Rejected)
                    .SetProperty(a => a.UpdatedAt, now));

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new SelectGuideApplicationResultDto
            {
                Success = true,
                Error = GuideApplicationError.None,
                Message = "Guide selected successfully.",
                ApplicationId = application.Id,
                GuideId = application.GuideId,
                GuideName = application.Guide.FullName,
                TripId = tripId,
                TripStatus = OrganizedTripStatus.RegistrationOpen.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to select guide application {ApplicationId} for trip {TripId} by organizer {OrganizerUserId}.",
                applicationId,
                tripId,
                organizerUserId);

            return new SelectGuideApplicationResultDto
            {
                Success = false,
                Error = GuideApplicationError.ServerError,
                Message = "Failed to select guide. Please try again."
            };
        }
    }

    private async Task<GuideVerification?> GetGuideVerificationAsync(int guideUserId)
    {
        var guideUserExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u =>
                u.Id == guideUserId &&
                u.Role == UserRole.Guide &&
                u.IsActive);

        if (!guideUserExists)
        {
            return null;
        }

        var status = await _context.GuideProfiles
            .AsNoTracking()
            .Where(p => p.UserId == guideUserId)
            .Select(p => (VerificationStatus?)p.VerificationStatus)
            .FirstOrDefaultAsync()
            ?? VerificationStatus.Pending;

        return new GuideVerification(
            status,
            status == VerificationStatus.Verified);
    }

    private sealed record GuideVerification(
        VerificationStatus Status,
        bool IsVerified)
    {
        public string Name => Status.ToString();
    }
}
