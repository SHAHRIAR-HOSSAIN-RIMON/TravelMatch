using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

/// <summary>
/// Provides cost-breakdown and cost-estimate endpoints for trip payment flows.
/// </summary>
[ApiController]
[Route("api/cost-breakdown")]
public class CostBreakdownController : ControllerBase
{
    private readonly ITripCostService _tripCostService;

    public CostBreakdownController(ITripCostService tripCostService)
    {
        _tripCostService = tripCostService;
    }

    // ── Proposal breakdown (Tourist or Guide) ─────────────────────────────────

    /// <summary>
    /// Returns the itemised cost breakdown for an accepted guide proposal.
    /// <para>
    /// Accessible to the tourist who owns the trip request, or the guide who submitted the proposal.
    /// </para>
    /// <para>
    /// <b>Breakdown rules (model-derived):</b><br/>
    /// • <c>BasePrice</c> = <c>Proposal.ProposedPrice</c> (guide's quoted group price).<br/>
    /// • <c>PricePerPerson</c> = <c>BasePrice / TripRequest.NumberOfTravelers</c> (rounded 2 dp).<br/>
    /// • <c>TotalAmount</c> = <c>BasePrice</c> (the full amount held in escrow on payment).
    /// </para>
    /// </summary>
    [HttpGet("proposal/{proposalId:int}")]
    [Authorize(Roles = "Tourist,Guide")]
    public async Task<IActionResult> GetProposalBreakdown([FromRoute] int proposalId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _tripCostService.GetProposalBreakdownAsync(proposalId, userId);
        return result.Success ? Ok(result) : MapError(result);
    }

    // ── Registration breakdown (Tourist only) ─────────────────────────────────

    /// <summary>
    /// Returns the itemised cost breakdown for an organised-trip registration.
    /// <para>Only the tourist who owns the registration may call this endpoint.</para>
    /// <para>
    /// <b>Breakdown rules (model-derived):</b><br/>
    /// • <c>BasePrice</c> = <c>OrganizedTrip.PricePerPerson</c>.<br/>
    /// • <c>PricePerPerson</c> = <c>BasePrice</c> (already per-seat).<br/>
    /// • <c>TotalAmount</c> = <c>PricePerPerson × Registration.NumberOfTravelers</c>
    ///   (matches <c>Registration.TotalPrice</c>).
    /// </para>
    /// </summary>
    [HttpGet("registration/{registrationId:int}")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> GetRegistrationBreakdown([FromRoute] int registrationId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _tripCostService.GetRegistrationBreakdownAsync(registrationId, userId);
        return result.Success ? Ok(result) : MapError(result);
    }

    // ── Pre-registration estimate (open to authenticated users) ──────────────

    /// <summary>
    /// Returns a cost estimate for an organised trip before the tourist has registered.
    /// <para>
    /// Pass <c>travelers</c> as a query parameter (must be ≥ 1) to compute the projected total.
    /// No registration record is required.
    /// </para>
    /// <para>
    /// <b>Estimate rules (model-derived):</b><br/>
    /// • <c>BasePrice</c> = <c>OrganizedTrip.PricePerPerson</c>.<br/>
    /// • <c>TotalAmount</c> = <c>PricePerPerson × travelers</c>.
    /// </para>
    /// </summary>
    [HttpGet("organized-trip/{organizedTripId:int}/estimate")]
    [Authorize]
    public async Task<IActionResult> EstimateOrganizedTripCost(
        [FromRoute] int organizedTripId,
        [FromQuery] int travelers = 1)
    {
        if (travelers < 1)
            return BadRequest(new CostBreakdownResultDto
            {
                Success = false,
                Error   = CostBreakdownError.InvalidRequest,
                Message = "The 'travelers' query parameter must be at least 1."
            });

        var result = await _tripCostService.EstimateOrganizedTripCostAsync(organizedTripId, travelers);
        return result.Success ? Ok(result) : MapError(result);
    }

    // ── Error mapping ─────────────────────────────────────────────────────────

    private IActionResult MapError(CostBreakdownResultDto result) => result.Error switch
    {
        CostBreakdownError.Unauthorized          => StatusCode(StatusCodes.Status403Forbidden, result),
        CostBreakdownError.ProposalNotFound      => NotFound(result),
        CostBreakdownError.OrganizedTripNotFound => NotFound(result),
        CostBreakdownError.RegistrationNotFound  => NotFound(result),
        CostBreakdownError.InvalidRequest        => BadRequest(result),
        CostBreakdownError.ServerError           => StatusCode(StatusCodes.Status500InternalServerError, result),
        _                                        => BadRequest(result)
    };

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}
