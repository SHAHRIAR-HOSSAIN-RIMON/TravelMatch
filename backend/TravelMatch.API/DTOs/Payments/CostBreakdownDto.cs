namespace TravelMatch.API.DTOs.Payments;

/// <summary>
/// Itemised cost breakdown for a trip — either a guide proposal or an organised-trip
/// registration. All amounts are in <see cref="Currency"/> (currently always "USD").
/// </summary>
public class CostBreakdownDto
{
    // ── Core identity ─────────────────────────────────────────────────────────

    /// <summary>Which trip-payment path produced this breakdown.</summary>
    public CostBreakdownType BreakdownType { get; init; }

    /// <summary>Always "USD" — matches the Payment model.</summary>
    public string Currency { get; init; } = "USD";

    // ── Proposal flow fields (populated only when BreakdownType == Proposal) ──

    /// <summary>Id of the proposal. Null for the Registration path.</summary>
    public int? ProposalId { get; init; }

    /// <summary>Id of the trip request the proposal was submitted against. Null for Registration path.</summary>
    public int? TripRequestId { get; init; }

    // ── Registration flow fields (populated when BreakdownType == Registration) ─

    /// <summary>Id of the organised trip. Null for the Proposal path.</summary>
    public int? OrganizedTripId { get; init; }

    /// <summary>Id of the registration. Null for the Proposal path.</summary>
    public int? RegistrationId { get; init; }

    // ── Shared price fields ───────────────────────────────────────────────────

    /// <summary>
    /// Proposal path : <c>Proposal.ProposedPrice</c> — the guide's total quoted price for the group.<br/>
    /// Registration path : <c>OrganizedTrip.PricePerPerson</c> — the per-seat list price.
    /// </summary>
    public decimal BasePrice { get; init; }

    /// <summary>Number of travellers this breakdown is calculated for.</summary>
    public int NumberOfTravelers { get; init; }

    /// <summary>
    /// Proposal path : <c>BasePrice / NumberOfTravelers</c> (rounded to 2 dp).<br/>
    /// Registration path : same as <c>BasePrice</c> (already per-person).
    /// </summary>
    public decimal PricePerPerson { get; init; }

    /// <summary>
    /// The total amount the tourist will be charged.<br/>
    /// Proposal path : <c>BasePrice</c> (the whole group price is a single flat amount).<br/>
    /// Registration path : <c>PricePerPerson × NumberOfTravelers</c>.
    /// </summary>
    public decimal TotalAmount { get; init; }

    // ── Human-readable context ────────────────────────────────────────────────

    /// <summary>What is included in the price (copied from Proposal or OrganizedTrip).</summary>
    public string Inclusions { get; init; } = string.Empty;

    /// <summary>What is NOT included (copied from Proposal or OrganizedTrip; may be empty).</summary>
    public string Exclusions { get; init; } = string.Empty;
}

/// <summary>Identifies which trip-payment flow produced a <see cref="CostBreakdownDto"/>.</summary>
public enum CostBreakdownType
{
    /// <summary>Cost breakdown for an accepted guide proposal (custom trip).</summary>
    Proposal,

    /// <summary>Cost breakdown for a registration on an organised group trip.</summary>
    Registration
}
