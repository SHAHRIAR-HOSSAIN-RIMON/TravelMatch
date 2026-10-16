using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Disputes;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class DisputeService : IDisputeService
{
    private readonly ApplicationDbContext _context;

    public DisputeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DisputeResultDto> RaiseDisputeAsync(int userId, UserRole userRole, RaiseDisputeDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.Description))
            return new DisputeResultDto { Success = false, Error = DisputeError.InvalidInput, Message = "Reason and description are required." };

        // Check the trip exists and the user is authorized to dispute it
        if (request.TripType == DisputeRelatedTripType.TripRequest)
        {
            var trip = await _context.TripRequests.FirstOrDefaultAsync(t => t.Id == request.TripId);

            if (trip == null)
                return new DisputeResultDto { Success = false, Error = DisputeError.TripNotFound, Message = "Trip request not found." };

            // Tourist must own the trip request; Guide must have a proposal for it
            bool authorized = userRole == UserRole.Tourist && trip.TouristId == userId
                || userRole == UserRole.Guide && await _context.Proposals.AnyAsync(p => p.TripRequestId == request.TripId && p.GuideId == userId);

            if (!authorized)
                return new DisputeResultDto { Success = false, Error = DisputeError.NotAuthorized, Message = "You are not a participant in this trip." };
        }
        else
        {
            var trip = await _context.OrganizedTrips.FirstOrDefaultAsync(t => t.Id == request.TripId);

            if (trip == null)
                return new DisputeResultDto { Success = false, Error = DisputeError.TripNotFound, Message = "Organized trip not found." };

            // Tourist must have a confirmed registration; Organizer must own the trip; Guide must be the selected guide
            bool authorized = userRole == UserRole.Tourist && await _context.Registrations.AnyAsync(r => r.OrganizedTripId == request.TripId && _context.TouristProfiles.Any(tp => tp.UserId == userId && tp.Id == r.TouristId))
                || userRole == UserRole.Organizer && _context.OrganizerProfiles.Any(op => op.UserId == userId && op.Id == trip.OrganizerId)
                || userRole == UserRole.Guide && trip.SelectedGuideId == userId;

            if (!authorized)
                return new DisputeResultDto { Success = false, Error = DisputeError.NotAuthorized, Message = "You are not a participant in this trip." };
        }

        var dispute = new Dispute
        {
            ReportedByUserId = userId,
            TripType = request.TripType,
            TripId = request.TripId,
            Reason = request.Reason,
            Description = request.Description,
            Status = DisputeStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        return new DisputeResultDto
        {
            Success = true,
            Message = "Dispute raised successfully.",
            Data = ToResponse(dispute)
        };
    }

    public async Task<DisputeResultDto> ResolveDisputeAsync(int adminUserId, int disputeId, string? resolutionNote)
    {
        if (disputeId <= 0)
            return new DisputeResultDto { Success = false, Error = DisputeError.InvalidInput, Message = "Invalid dispute identifier." };

        if (string.IsNullOrWhiteSpace(resolutionNote))
            return new DisputeResultDto { Success = false, Error = DisputeError.InvalidInput, Message = "A resolution note is required." };

        var trimmedNote = resolutionNote.Trim();
        if (trimmedNote.Length > 2000)
            return new DisputeResultDto { Success = false, Error = DisputeError.InvalidInput, Message = "Resolution note must be 2000 characters or fewer." };

        // Enforced here as well as by the endpoint's [Authorize(Roles = "Admin")] so a
        // non-controller caller cannot bypass the admin rule.
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Id == adminUserId);
        if (admin is null || admin.Role != UserRole.Admin)
            return new DisputeResultDto { Success = false, Error = DisputeError.AdminOnly, Message = "Only an administrator may resolve a dispute." };

        var dispute = await _context.Disputes.FirstOrDefaultAsync(d => d.Id == disputeId);
        if (dispute is null)
            return new DisputeResultDto { Success = false, Error = DisputeError.DisputeNotFound, Message = "Dispute not found." };

        if (dispute.Status != DisputeStatus.Open)
            return new DisputeResultDto
            {
                Success = false,
                Error = DisputeError.DisputeAlreadyResolved,
                Message = $"Only an open dispute can be resolved. This dispute is {dispute.Status}."
            };

        dispute.Status = DisputeStatus.Resolved;
        dispute.ResolutionNote = trimmedNote;
        dispute.ResolvedByUserId = admin.Id;
        dispute.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new DisputeResultDto
        {
            Success = true,
            Message = "Dispute resolved successfully.",
            Data = ToResponse(dispute)
        };
    }

    private static DisputeResponseDto ToResponse(Dispute dispute) => new()
    {
        Id = dispute.Id,
        ReportedByUserId = dispute.ReportedByUserId,
        TripType = dispute.TripType.ToString(),
        TripId = dispute.TripId,
        Reason = dispute.Reason,
        Description = dispute.Description,
        Status = dispute.Status.ToString(),
        CreatedAt = dispute.CreatedAt,
        ResolutionNote = dispute.ResolutionNote,
        ResolvedByUserId = dispute.ResolvedByUserId,
        ResolvedAt = dispute.ResolvedAt
    };
}
