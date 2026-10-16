using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // ── Deposit endpoints (Tourist only) ─────────────────────────────────────

    /// <summary>
    /// Simulate a deposit for an accepted guide proposal.
    /// Funds are placed into simulated escrow (status → Held).
    /// </summary>
    [HttpPost("proposal/{proposalId:int}")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> PayForProposal(
        [FromRoute] int proposalId,
        [FromBody] PayProposalRequestDto? request = null)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _paymentService.PayForProposalAsync(userId, proposalId, request);
        return result.Success ? Ok(result) : MapError(result);
    }

    /// <summary>
    /// Simulate a deposit for an organized-trip registration.
    /// Funds are placed into simulated escrow (status → Held) and the registration is confirmed.
    /// </summary>
    [HttpPost("registration/{registrationId:int}")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> PayForRegistration(
        [FromRoute] int registrationId,
        [FromBody] PayRegistrationRequestDto? request = null)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _paymentService.PayForRegistrationAsync(userId, registrationId, request);
        return result.Success ? Ok(result) : MapError(result);
    }

    // ── Escrow release (Tourist) ──────────────────────────────────────────────

    /// <summary>
    /// Release held escrow to the guide/organizer after the trip has completed.
    /// Only the tourist who made the deposit may call this.
    /// Blocked when an open dispute exists on the associated trip.
    /// </summary>
    [HttpPost("{paymentId:int}/release")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> ReleaseEscrow([FromRoute] int paymentId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _paymentService.ReleaseEscrowAsync(userId, paymentId);
        return result.Success ? Ok(result) : MapError(result);
    }

    /// <summary>
    /// Release a single milestone of a milestone-allocated payment.
    /// Only the tourist who made the deposit may call this, and only after the
    /// associated trip has completed. Blocked when an open dispute exists.
    /// </summary>
    [HttpPost("{paymentId:int}/milestones/{milestoneId:int}/release")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> ReleaseMilestone(
        [FromRoute] int paymentId,
        [FromRoute] int milestoneId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _paymentService.ReleaseMilestoneAsync(userId, paymentId, milestoneId);
        return result.Success ? Ok(result) : MapError(result);
    }

    // ── Refund (Admin only) ───────────────────────────────────────────────────

    /// <summary>
    /// Issue a full or partial refund on a held payment.
    /// Only Administrators may call this endpoint.
    /// Amount must be greater than zero; omit it to refund the full remaining balance.
    /// Refunds are refused while an open dispute exists on the associated trip.
    /// </summary>
    [HttpPost("{paymentId:int}/refund")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Refund(
        [FromRoute] int paymentId,
        [FromBody] RefundRequestDto request)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _paymentService.RefundAsync(
            adminUserId, paymentId, request.Amount, request.Reason);

        return result.Success ? Ok(result) : MapError(result);
    }

    // ── Tourist: view own payments ────────────────────────────────────────────

    /// <summary>
    /// Returns all payment records owned by the authenticated tourist,
    /// ordered by creation date descending.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> GetMyPayments()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _paymentService.GetMyPaymentsAsync(userId);
        return Ok(result);
    }

    // ── Organizer: view group-trip payment states ─────────────────────────────

    /// <summary>
    /// Returns all payment records for registrations on a specific organized trip.
    /// Caller must be the organizer who owns the trip.
    /// </summary>
    [HttpGet("trip/{organizedTripId:int}")]
    [Authorize(Roles = "Organizer")]
    public async Task<IActionResult> GetTripPayments([FromRoute] int organizedTripId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _paymentService.GetTripPaymentsAsync(userId, organizedTripId);
        return result.Success ? Ok(result) : MapListError(result);
    }

    // ── Admin: view payments by user ──────────────────────────────────────────

    /// <summary>
    /// Returns all payment records for a specific user — for dispute resolution and audit.
    /// Only Administrators may call this endpoint.
    /// </summary>
    [HttpGet("user/{targetUserId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPaymentsByUser([FromRoute] int targetUserId)
    {
        var result = await _paymentService.GetPaymentsByUserAsync(targetUserId);
        return Ok(result);
    }

    // ── Error mapping ─────────────────────────────────────────────────────────

    private IActionResult MapError(PaymentResultDto result) => result.Error switch
    {
        PaymentError.Unauthorized              => StatusCode(StatusCodes.Status403Forbidden, result),
        PaymentError.TouristProfileNotFound    => StatusCode(StatusCodes.Status403Forbidden, result),
        PaymentError.OrganizerProfileNotFound  => StatusCode(StatusCodes.Status403Forbidden, result),
        PaymentError.ProposalNotFound          => NotFound(result),
        PaymentError.TripRequestNotFound       => NotFound(result),
        PaymentError.RegistrationNotFound      => NotFound(result),
        PaymentError.OrganizedTripNotFound     => NotFound(result),
        PaymentError.PaymentNotFound           => NotFound(result),
        PaymentError.AlreadyPaid               => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.InvalidProposalStatus     => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.InvalidRegistrationStatus => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.TripCapacityExceeded      => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.RegistrationDeadlinePassed=> StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.TripNotAvailableForRegistration => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.EscrowAlreadySettled      => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.DisputeBlocksRelease      => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.DisputeResolutionRequired  => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.MilestoneAlreadySettled    => StatusCode(StatusCodes.Status409Conflict, result),
        PaymentError.MilestoneNotFound         => NotFound(result),
        PaymentError.TripNotCompleted          => StatusCode(StatusCodes.Status422UnprocessableEntity, result),
        PaymentError.NotRefundable             => StatusCode(StatusCodes.Status422UnprocessableEntity, result),
        PaymentError.RefundExceedsHeldAmount   => BadRequest(result),
        PaymentError.InvalidMilestone          => BadRequest(result),
        PaymentError.MilestoneAmountMismatch   => BadRequest(result),
        PaymentError.PaymentFailed             => StatusCode(StatusCodes.Status402PaymentRequired, result),
        PaymentError.InvalidRequest            => BadRequest(result),
        PaymentError.ServerError               => StatusCode(StatusCodes.Status500InternalServerError, result),
        _                                      => BadRequest(result)
    };

    private IActionResult MapListError(PaymentListResultDto result) => result.Error switch
    {
        PaymentError.Unauthorized             => StatusCode(StatusCodes.Status403Forbidden, result),
        PaymentError.OrganizerProfileNotFound => StatusCode(StatusCodes.Status403Forbidden, result),
        PaymentError.OrganizedTripNotFound    => NotFound(result),
        _                                     => BadRequest(result)
    };

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}
