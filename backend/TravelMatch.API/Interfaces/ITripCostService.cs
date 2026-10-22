using TravelMatch.API.DTOs.Payments;

namespace TravelMatch.API.Interfaces;

/// <summary>
/// Provides cost-breakdown calculations for trip payment flows.
/// Implementations load the required entities and delegate the arithmetic to
/// <see cref="TravelMatch.API.Services.TripCostCalculator"/>.
/// </summary>
public interface ITripCostService
{
    /// <summary>
    /// Returns the cost breakdown for an accepted guide proposal.
    /// The breakdown is accessible to the tourist who owns the trip request,
    /// or to the guide who submitted the proposal.
    /// </summary>
    /// <param name="proposalId">Id of the proposal.</param>
    /// <param name="requestingUserId">Id of the authenticated user making the request.</param>
    Task<CostBreakdownResultDto> GetProposalBreakdownAsync(int proposalId, int requestingUserId);

    /// <summary>
    /// Returns the cost breakdown for an organised-trip registration.
    /// The breakdown is accessible to the tourist who owns the registration.
    /// </summary>
    /// <param name="registrationId">Id of the registration.</param>
    /// <param name="requestingUserId">Id of the authenticated tourist.</param>
    Task<CostBreakdownResultDto> GetRegistrationBreakdownAsync(int registrationId, int requestingUserId);

    /// <summary>
    /// Returns a cost estimate for an organised trip before the tourist has registered.
    /// This is a public/unauthenticated-friendly calculation: no registration record is required.
    /// </summary>
    /// <param name="organizedTripId">Id of the organised trip.</param>
    /// <param name="numberOfTravelers">Number of travellers to price (≥ 1).</param>
    Task<CostBreakdownResultDto> EstimateOrganizedTripCostAsync(int organizedTripId, int numberOfTravelers);
}
