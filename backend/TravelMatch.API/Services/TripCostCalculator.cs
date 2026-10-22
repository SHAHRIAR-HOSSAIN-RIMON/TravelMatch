using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

/// <summary>
/// Pure-logic, stateless trip cost calculator.
/// <para>
/// All computation rules are derived directly from the data model:
/// <list type="bullet">
///   <item>
///     <b>Proposal path</b> — the guide quotes a single <c>ProposedPrice</c> that covers the
///     entire group.  The total charged equals <c>ProposedPrice</c>; the per-person figure is
///     <c>ProposedPrice / NumberOfTravelers</c>, rounded to two decimal places.
///   </item>
///   <item>
///     <b>Registration path</b> — the organised trip has a <c>PricePerPerson</c> rate.
///     <c>TotalAmount = PricePerPerson × NumberOfTravelers</c>, which mirrors the value stored in
///     <c>Registration.TotalPrice</c> by <c>RegistrationService</c>.
///   </item>
/// </list>
/// No platform fees, taxes, commissions, or surcharges exist in the data model; none are applied.
/// </para>
/// </summary>
public static class TripCostCalculator
{
    // ── Proposal breakdown ────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the cost breakdown for a guide proposal.
    /// </summary>
    /// <param name="proposal">The accepted proposal (must have <see cref="Proposal.TripRequest"/> loaded).</param>
    /// <returns>An itemised <see cref="CostBreakdownDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="proposal"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when <c>ProposedPrice</c> ≤ 0 or <c>NumberOfTravelers</c> &lt; 1.
    /// </exception>
    public static CostBreakdownDto CalculateForProposal(Proposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (proposal.ProposedPrice <= 0m)
            throw new ArgumentException(
                "ProposedPrice must be greater than zero.", nameof(proposal));

        var travelers = proposal.TripRequest?.NumberOfTravelers ?? 0;
        if (travelers < 1)
            throw new ArgumentException(
                "NumberOfTravelers on the associated TripRequest must be at least 1.", nameof(proposal));

        // Per-person: full group price split evenly, rounded to 2 dp.
        var perPerson = Math.Round(proposal.ProposedPrice / travelers, 2, MidpointRounding.AwayFromZero);

        return new CostBreakdownDto
        {
            BreakdownType    = CostBreakdownType.Proposal,
            Currency         = "USD",
            ProposalId       = proposal.Id,
            TripRequestId    = proposal.TripRequestId,
            BasePrice        = proposal.ProposedPrice,
            NumberOfTravelers = travelers,
            PricePerPerson   = perPerson,
            // The tourist pays the full ProposedPrice as a single escrow deposit.
            TotalAmount      = proposal.ProposedPrice,
            Inclusions       = proposal.Inclusions,
            Exclusions       = proposal.Exclusions
        };
    }

    // ── Registration breakdown ────────────────────────────────────────────────

    /// <summary>
    /// Calculates the cost breakdown for an organised-trip registration.
    /// </summary>
    /// <param name="organizedTrip">The organised trip whose <c>PricePerPerson</c> is the unit price.</param>
    /// <param name="numberOfTravelers">The number of travellers in the registration (≥ 1).</param>
    /// <param name="registrationId">Optional registration Id to embed in the response.</param>
    /// <returns>An itemised <see cref="CostBreakdownDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="organizedTrip"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when <c>PricePerPerson</c> ≤ 0 or <paramref name="numberOfTravelers"/> &lt; 1.
    /// </exception>
    public static CostBreakdownDto CalculateForRegistration(
        OrganizedTrip organizedTrip,
        int numberOfTravelers,
        int? registrationId = null)
    {
        ArgumentNullException.ThrowIfNull(organizedTrip);

        if (organizedTrip.PricePerPerson <= 0m)
            throw new ArgumentException(
                "PricePerPerson must be greater than zero.", nameof(organizedTrip));

        if (numberOfTravelers < 1)
            throw new ArgumentOutOfRangeException(
                nameof(numberOfTravelers), "Number of travelers must be at least 1.");

        // Total mirrors Registration.TotalPrice = PricePerPerson * NumberOfTravelers.
        var total = organizedTrip.PricePerPerson * numberOfTravelers;

        return new CostBreakdownDto
        {
            BreakdownType    = CostBreakdownType.Registration,
            Currency         = "USD",
            OrganizedTripId  = organizedTrip.Id,
            RegistrationId   = registrationId,
            BasePrice        = organizedTrip.PricePerPerson,
            NumberOfTravelers = numberOfTravelers,
            // For organised trips the BasePrice is already per-person.
            PricePerPerson   = organizedTrip.PricePerPerson,
            TotalAmount      = total,
            Inclusions       = organizedTrip.Inclusions,
            Exclusions       = organizedTrip.Exclusions ?? string.Empty
        };
    }
}
