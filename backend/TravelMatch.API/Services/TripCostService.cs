using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

/// <summary>
/// DB-backed implementation of <see cref="ITripCostService"/>.
/// Loads entities from the database, authorises the request, then delegates
/// arithmetic to the stateless <see cref="TripCostCalculator"/>.
/// </summary>
public class TripCostService : ITripCostService
{
    private readonly ApplicationDbContext _context;

    public TripCostService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CostBreakdownResultDto Failure(CostBreakdownError error, string message) =>
        new() { Success = false, Error = error, Message = message };

    // ── Proposal breakdown ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<CostBreakdownResultDto> GetProposalBreakdownAsync(
        int proposalId,
        int requestingUserId)
    {
        if (proposalId <= 0)
            return Failure(CostBreakdownError.InvalidRequest, "Invalid proposal identifier.");

        var proposal = await _context.Proposals
            .Include(p => p.TripRequest)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal is null)
            return Failure(CostBreakdownError.ProposalNotFound, "Proposal not found.");

        if (proposal.TripRequest is null)
            return Failure(CostBreakdownError.ProposalNotFound,
                "The trip request associated with this proposal was not found.");

        // Authorise: the tourist who owns the trip request, or the guide who submitted the proposal.
        var isTouristOwner = proposal.TripRequest.TouristId == requestingUserId;
        var isProposalGuide = proposal.GuideId == requestingUserId;
        if (!isTouristOwner && !isProposalGuide)
            return Failure(CostBreakdownError.Unauthorized,
                "You are not authorised to view this cost breakdown.");

        // Validate data integrity before calculating.
        if (proposal.ProposedPrice <= 0m)
            return Failure(CostBreakdownError.InvalidRequest,
                "Proposal price is invalid; the guide must set a price greater than zero.");

        if (proposal.TripRequest.NumberOfTravelers < 1)
            return Failure(CostBreakdownError.InvalidRequest,
                "Trip request must specify at least one traveller.");

        var breakdown = TripCostCalculator.CalculateForProposal(proposal);

        return new CostBreakdownResultDto
        {
            Success = true,
            Error   = CostBreakdownError.None,
            Message = "Cost breakdown calculated successfully.",
            Data    = breakdown
        };
    }

    // ── Registration breakdown ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<CostBreakdownResultDto> GetRegistrationBreakdownAsync(
        int registrationId,
        int requestingUserId)
    {
        if (registrationId <= 0)
            return Failure(CostBreakdownError.InvalidRequest, "Invalid registration identifier.");

        // Resolve tourist profile — needed to match registration ownership.
        var touristProfile = await _context.TouristProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(tp => tp.UserId == requestingUserId);

        if (touristProfile is null)
            return Failure(CostBreakdownError.Unauthorized,
                "No tourist profile found for the authenticated user.");

        var registration = await _context.Registrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == registrationId);

        if (registration is null)
            return Failure(CostBreakdownError.RegistrationNotFound, "Registration not found.");

        if (registration.TouristId != touristProfile.Id)
            return Failure(CostBreakdownError.Unauthorized,
                "You are not authorised to view this cost breakdown.");

        var organizedTrip = await _context.OrganizedTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == registration.OrganizedTripId);

        if (organizedTrip is null)
            return Failure(CostBreakdownError.OrganizedTripNotFound,
                "The organised trip associated with this registration was not found.");

        if (organizedTrip.PricePerPerson <= 0m)
            return Failure(CostBreakdownError.InvalidRequest,
                "Trip price per person is invalid.");

        if (registration.NumberOfTravelers < 1)
            return Failure(CostBreakdownError.InvalidRequest,
                "Registration must specify at least one traveller.");

        var breakdown = TripCostCalculator.CalculateForRegistration(
            organizedTrip,
            registration.NumberOfTravelers,
            registrationId: registration.Id);

        return new CostBreakdownResultDto
        {
            Success = true,
            Error   = CostBreakdownError.None,
            Message = "Cost breakdown calculated successfully.",
            Data    = breakdown
        };
    }

    // ── Pre-registration estimate ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<CostBreakdownResultDto> EstimateOrganizedTripCostAsync(
        int organizedTripId,
        int numberOfTravelers)
    {
        if (organizedTripId <= 0)
            return Failure(CostBreakdownError.InvalidRequest, "Invalid organised-trip identifier.");

        if (numberOfTravelers < 1)
            return Failure(CostBreakdownError.InvalidRequest,
                "Number of travelers must be at least 1.");

        var organizedTrip = await _context.OrganizedTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == organizedTripId);

        if (organizedTrip is null)
            return Failure(CostBreakdownError.OrganizedTripNotFound, "Organised trip not found.");

        if (organizedTrip.PricePerPerson <= 0m)
            return Failure(CostBreakdownError.InvalidRequest,
                "Trip price per person is invalid.");

        var breakdown = TripCostCalculator.CalculateForRegistration(
            organizedTrip,
            numberOfTravelers,
            registrationId: null);

        return new CostBreakdownResultDto
        {
            Success = true,
            Error   = CostBreakdownError.None,
            Message = "Cost estimate calculated successfully.",
            Data    = breakdown
        };
    }
}
