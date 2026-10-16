using TravelMatch.API.DTOs.Payments;

namespace TravelMatch.API.Interfaces;

public interface IPaymentService
{
    // ── Deposit (creates escrow hold) ────────────────────────────────────────
    Task<PaymentResultDto> PayForProposalAsync(int touristUserId, int proposalId, PayProposalRequestDto? request = null);
    Task<PaymentResultDto> PayForRegistrationAsync(int touristUserId, int registrationId, PayRegistrationRequestDto? request = null);

    // ── Escrow lifecycle ─────────────────────────────────────────────────────
    /// <summary>
    /// Release held escrow to the guide/organizer. Only the tourist who owns the payment
    /// may release it, and only after the associated trip has reached its completed state
    /// (TripRequest.Completed or OrganizedTrip.Completed). Trip completion enables release;
    /// it never moves funds by itself. Release is refused while an open dispute exists on
    /// the associated trip, leaving both trip and payment state untouched.
    /// </summary>
    Task<PaymentResultDto> ReleaseEscrowAsync(int touristUserId, int paymentId);

    /// <summary>
    /// Release a single milestone of a milestone-allocated payment. Subject to the same
    /// completion and open-dispute rules as <see cref="ReleaseEscrowAsync"/>. The parent
    /// payment becomes Released only once every one of its milestones is settled.
    /// </summary>
    Task<PaymentResultDto> ReleaseMilestoneAsync(int touristUserId, int paymentId, int milestoneId);

    /// <summary>
    /// Issue a full or partial refund on a Held or PartiallyRefunded payment. The endpoint
    /// is Administrator-only; the service additionally refuses refunds while an open dispute
    /// exists on the associated trip, and never refunds amounts already paid out through
    /// released milestones or earlier refunds. Refunds are not issued automatically by any
    /// cancellation path — an administrator must invoke this explicitly.
    /// </summary>
    Task<PaymentResultDto> RefundAsync(int adminUserId, int paymentId, decimal? amount, string? reason);

    // ── Read / status ─────────────────────────────────────────────────────────
    /// <summary>All payment records owned by the authenticated tourist.</summary>
    Task<PaymentListResultDto> GetMyPaymentsAsync(int touristUserId);

    /// <summary>
    /// All payment records for registrations on a given organized trip.
    /// Caller must be the organizer who owns the trip.
    /// </summary>
    Task<PaymentListResultDto> GetTripPaymentsAsync(int organizerUserId, int organizedTripId);

    /// <summary>
    /// All payment records for a given user — admin view.
    /// </summary>
    Task<PaymentListResultDto> GetPaymentsByUserAsync(int targetUserId);
}
