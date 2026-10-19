using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TravelMatch.API.Data;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ISandboxPaymentGateway _paymentGateway;
    private readonly ILogger<PaymentService> _logger;
    private readonly INotificationService _notificationService;

    public PaymentService(
        ApplicationDbContext context,
        ISandboxPaymentGateway paymentGateway,
        ILogger<PaymentService> logger,
        INotificationService notificationService)
    {
        _context = context;
        _paymentGateway = paymentGateway;
        _logger = logger;
        _notificationService = notificationService;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PaymentResultDto Failure(PaymentError error, string message) =>
        new() { Success = false, Error = error, Message = message };

    private static PaymentMilestoneResponseDto ToMilestoneDto(PaymentMilestone m) => new()
    {
        MilestoneId = m.Id,
        Index       = m.Index,
        Label       = m.Label,
        Amount      = m.Amount,
        Status      = m.Status,
        CreatedAt   = m.CreatedAt,
        HeldAt      = m.HeldAt,
        ReleasedAt  = m.ReleasedAt,
        RefundedAt  = m.RefundedAt
    };

    private static PaymentResponseDto ToResponseDto(Payment p) => new()
    {
        PaymentId              = p.Id,
        TouristUserId          = p.TouristUserId,
        TargetType             = p.TargetType,
        ProposalId             = p.ProposalId,
        RegistrationId         = p.RegistrationId,
        Amount                 = p.Amount,
        Currency               = p.Currency,
        Status                 = p.Status,
        TransactionReference   = p.TransactionReference,
        FailureReason          = p.FailureReason,
        CreatedAt              = p.CreatedAt,
        CompletedAt            = p.CompletedAt,
        EscrowReleasedAt       = p.EscrowReleasedAt,
        RefundedAmount         = p.RefundedAmount,
        RefundedAt             = p.RefundedAt,
        RefundReason           = p.RefundReason,
        Milestones             = p.Milestones.Select(ToMilestoneDto).ToList()
    };

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx &&
            pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
            return true;

        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key",     StringComparison.OrdinalIgnoreCase)
            || msg.Contains("IX_Payments_ProposalId_Escrow_Unique",      StringComparison.OrdinalIgnoreCase)
            || msg.Contains("IX_Payments_RegistrationId_Escrow_Unique",  StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates a requested milestone split against the payment amount.
    /// Returns null when the split is usable, otherwise a typed failure.
    /// </summary>
    private static PaymentResultDto? ValidateMilestonePlan(
        List<CreatePaymentMilestoneDto>? plan,
        decimal paymentAmount)
    {
        if (plan is null || plan.Count == 0)
            return null;

        if (plan.Any(m => m.Amount <= 0m))
            return Failure(PaymentError.InvalidMilestone,
                "Every milestone amount must be greater than zero.");

        if (plan.Any(m => string.IsNullOrWhiteSpace(m.Label)))
            return Failure(PaymentError.InvalidMilestone,
                "Every milestone must have a label.");

        if (plan.Select(m => m.Index).Distinct().Count() != plan.Count)
            return Failure(PaymentError.InvalidMilestone,
                "Milestone indexes must be unique within a payment.");

        var total = plan.Sum(m => m.Amount);
        if (total > paymentAmount)
            return Failure(PaymentError.MilestoneAmountMismatch,
                $"Milestone total ({total:F2}) exceeds the payment amount ({paymentAmount:F2}).");

        if (total != paymentAmount)
            return Failure(PaymentError.MilestoneAmountMismatch,
                $"Milestone total ({total:F2}) must equal the payment amount ({paymentAmount:F2}).");

        return null;
    }

    private static List<PaymentMilestone> BuildMilestones(
        List<CreatePaymentMilestoneDto> plan,
        decimal paymentAmount)
    {
        var now = DateTime.UtcNow;
        return plan
            .OrderBy(m => m.Index)
            .Select(m => new PaymentMilestone
            {
                Index      = m.Index,
                Label      = m.Label.Trim(),
                Amount     = m.Amount,
                Status     = PaymentMilestoneStatus.Held,
                CreatedAt  = now,
                HeldAt     = now
            })
            .ToList();
    }

    /// <summary>Escrow already paid out to the guide/organizer through released milestones.</summary>
    private static decimal ReleasedMilestoneTotal(Payment payment) =>
        payment.Milestones.Where(m => m.Status == PaymentMilestoneStatus.Released).Sum(m => m.Amount);

    /// <summary>
    /// Loads milestones for an untracked payment list. Used instead of Include because the
    /// organizer query projects through a join, where Include cannot be applied.
    /// </summary>
    private async Task AttachMilestonesAsync(List<Payment> payments)
    {
        if (payments.Count == 0)
            return;

        var paymentIds = payments.Select(p => p.Id).ToList();

        var milestones = await _context.PaymentMilestones
            .AsNoTracking()
            .Where(m => paymentIds.Contains(m.PaymentId))
            .OrderBy(m => m.PaymentId)
            .ThenBy(m => m.Index)
            .ToListAsync();

        foreach (var payment in payments)
            payment.Milestones = milestones.Where(m => m.PaymentId == payment.Id).ToList();
    }

    // ── Deposit: proposal ────────────────────────────────────────────────────

    public async Task<PaymentResultDto> PayForProposalAsync(
        int touristUserId,
        int proposalId,
        PayProposalRequestDto? request = null)
    {
        if (proposalId <= 0)
            return Failure(PaymentError.InvalidRequest, "Invalid proposal identifier.");

        try
        {
            var tx = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync() : null;
            await using var _ = tx;

            var proposal = await _context.Proposals
                .Include(p => p.TripRequest)
                .FirstOrDefaultAsync(p => p.Id == proposalId);

            if (proposal is null)
                return Failure(PaymentError.ProposalNotFound, "Proposal not found.");
            if (proposal.TripRequest is null)
                return Failure(PaymentError.TripRequestNotFound, "Associated trip request not found.");
            if (proposal.TripRequest.TouristId != touristUserId)
                return Failure(PaymentError.Unauthorized, "You are not authorized to pay for this proposal.");
            if (proposal.Status != ProposalStatus.Accepted)
                return Failure(PaymentError.InvalidProposalStatus, "Payments can only be made for accepted proposals.");

            // Block duplicate successful payments (Held, Released, Refunded, PartiallyRefunded, or legacy Completed)
            var existing = await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ProposalId == proposalId &&
                    (p.Status == PaymentStatus.Held
                     || p.Status == PaymentStatus.Released
                     || p.Status == PaymentStatus.Refunded
                     || p.Status == PaymentStatus.PartiallyRefunded
                     || p.Status == PaymentStatus.Completed));

            if (existing is not null)
                return new PaymentResultDto
                {
                    Success = false,
                    Error   = PaymentError.AlreadyPaid,
                    Message = "Payment has already been completed for this proposal.",
                    Data    = ToResponseDto(existing)
                };

            var paymentAmount = proposal.ProposedPrice;
            if (paymentAmount <= 0)
                return Failure(PaymentError.InvalidRequest, "Proposal price must be greater than zero.");

            var milestoneError = ValidateMilestonePlan(request?.Milestones, paymentAmount);
            if (milestoneError is not null)
                return milestoneError;

            var payment = new Payment
            {
                TouristUserId        = touristUserId,
                TargetType           = PaymentTargetType.Proposal,
                ProposalId           = proposalId,
                Amount               = paymentAmount,
                Currency             = "USD",
                Status               = PaymentStatus.Pending,
                CreatedAt            = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            var gatewayResult = await _paymentGateway.ProcessPaymentAsync(
                payment.Amount, payment.Currency, request?.PaymentMethod);

            payment.TransactionReference = gatewayResult.TransactionReference;

            if (gatewayResult.IsSuccess)
            {
                // Funds enter simulated escrow — status is Held, not Completed
                payment.Status      = PaymentStatus.Held;
                payment.CompletedAt = DateTime.UtcNow;
                payment.FailureReason = null;

                if (request?.Milestones is { Count: > 0 })
                    payment.Milestones.AddRange(BuildMilestones(request.Milestones, paymentAmount));

                await _context.SaveChangesAsync();
                if (tx is not null) await tx.CommitAsync();

                _logger.LogInformation(
                    "Proposal payment {PaymentId} held in escrow for Proposal {ProposalId}.",
                    payment.Id, proposalId);

                // Notify the guide that payment was received
                var guideUserId = proposal.GuideId;
                if (guideUserId > 0)
                {
                    await _notificationService.CreateNotificationAsync(
                        guideUserId,
                        "Payment received",
                        $"Payment of {payment.Amount:C} for your proposal on '{proposal.TripRequest.Destination}' has been deposited and held in escrow.");
                }

                return new PaymentResultDto
                {
                    Success = true,
                    Error   = PaymentError.None,
                    Message = "Payment deposited and held in escrow.",
                    Data    = ToResponseDto(payment)
                };
            }
            else
            {
                payment.Status        = PaymentStatus.Failed;
                payment.FailureReason = gatewayResult.FailureReason ?? "Payment declined.";
                await _context.SaveChangesAsync();
                if (tx is not null) await tx.CommitAsync();

                _logger.LogWarning(
                    "Proposal payment failed for Proposal {ProposalId}. Reason: {Reason}",
                    proposalId, payment.FailureReason);

                return new PaymentResultDto
                {
                    Success = false,
                    Error   = PaymentError.PaymentFailed,
                    Message = payment.FailureReason,
                    Data    = ToResponseDto(payment)
                };
            }
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex,
                "Unique constraint on proposal payment for Proposal {ProposalId}.", proposalId);
            return Failure(PaymentError.AlreadyPaid,
                "Payment has already been completed for this proposal.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing proposal payment for Proposal {ProposalId}.", proposalId);
            return Failure(PaymentError.ServerError,
                "An unexpected error occurred. Please retry.");
        }
    }

    // ── Deposit: registration ────────────────────────────────────────────────

    public async Task<PaymentResultDto> PayForRegistrationAsync(
        int touristUserId,
        int registrationId,
        PayRegistrationRequestDto? request = null)
    {
        if (registrationId <= 0)
            return Failure(PaymentError.InvalidRequest, "Invalid registration identifier.");

        try
        {
            var tx = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync() : null;
            await using var _ = tx;

            var touristProfile = await _context.TouristProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(tp => tp.UserId == touristUserId);

            if (touristProfile is null)
                return Failure(PaymentError.TouristProfileNotFound,
                    "Tourist profile not found for authenticated user.");

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration is null)
                return Failure(PaymentError.RegistrationNotFound, "Registration not found.");
            if (registration.TouristId != touristProfile.Id)
                return Failure(PaymentError.Unauthorized,
                    "You are not authorized to pay for this registration.");

            if (registration.Status == RegistrationStatus.Confirmed)
            {
                var existingPaid = await _context.Payments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p =>
                        p.RegistrationId == registrationId &&
                        (p.Status == PaymentStatus.Held
                         || p.Status == PaymentStatus.Released
                         || p.Status == PaymentStatus.Refunded
                         || p.Status == PaymentStatus.PartiallyRefunded
                         || p.Status == PaymentStatus.Completed));

                return new PaymentResultDto
                {
                    Success = false,
                    Error   = PaymentError.AlreadyPaid,
                    Message = "This registration has already been paid and confirmed.",
                    Data    = existingPaid is not null ? ToResponseDto(existingPaid) : null
                };
            }

            if (registration.Status == RegistrationStatus.Cancelled)
                return Failure(PaymentError.InvalidRegistrationStatus,
                    "Cannot pay for a cancelled registration.");

            // Row-lock the trip on PostgreSQL to prevent capacity races
            OrganizedTrip? organizedTrip;
            if (_context.Database.IsRelational())
            {
                organizedTrip = await _context.OrganizedTrips
                    .FromSqlInterpolated(
                        $"SELECT * FROM \"OrganizedTrips\" WHERE \"Id\" = {registration.OrganizedTripId} FOR UPDATE")
                    .FirstOrDefaultAsync();
            }
            else
            {
                organizedTrip = await _context.OrganizedTrips
                    .FirstOrDefaultAsync(t => t.Id == registration.OrganizedTripId);
            }

            if (organizedTrip is null)
                return Failure(PaymentError.OrganizedTripNotFound,
                    "Associated organized trip not found.");
            if (organizedTrip.Status != OrganizedTripStatus.RegistrationOpen)
                return Failure(PaymentError.TripNotAvailableForRegistration,
                    "The organized trip is not open for registration.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (organizedTrip.RegistrationDeadline < today)
                return Failure(PaymentError.RegistrationDeadlinePassed,
                    "The registration deadline for this trip has passed.");

            var confirmedCount = await _context.Registrations
                .CountAsync(r =>
                    r.OrganizedTripId == organizedTrip.Id &&
                    r.Status == RegistrationStatus.Confirmed &&
                    r.Id != registrationId);

            if (confirmedCount >= organizedTrip.MaxParticipants)
                return Failure(PaymentError.TripCapacityExceeded,
                    "This trip has reached its maximum participant capacity.");

            var paymentAmount = organizedTrip.PricePerPerson;
            if (paymentAmount <= 0)
                return Failure(PaymentError.InvalidRequest,
                    "Trip price per person must be greater than zero.");

            var milestoneError = ValidateMilestonePlan(request?.Milestones, paymentAmount);
            if (milestoneError is not null)
                return milestoneError;

            var payment = new Payment
            {
                TouristUserId  = touristUserId,
                TargetType     = PaymentTargetType.Registration,
                RegistrationId = registrationId,
                Amount         = paymentAmount,
                Currency       = "USD",
                Status         = PaymentStatus.Pending,
                CreatedAt      = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            var gatewayResult = await _paymentGateway.ProcessPaymentAsync(
                payment.Amount, payment.Currency, request?.PaymentMethod);

            payment.TransactionReference = gatewayResult.TransactionReference;

            if (gatewayResult.IsSuccess)
            {
                // Funds enter simulated escrow; confirm registration
                payment.Status      = PaymentStatus.Held;
                payment.CompletedAt = DateTime.UtcNow;
                payment.FailureReason = null;

                if (request?.Milestones is { Count: > 0 })
                    payment.Milestones.AddRange(BuildMilestones(request.Milestones, paymentAmount));

                registration.Status      = RegistrationStatus.Confirmed;
                registration.ConfirmedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                if (tx is not null) await tx.CommitAsync();

                _logger.LogInformation(
                    "Registration payment {PaymentId} held in escrow for Registration {RegistrationId}.",
                    payment.Id, registrationId);

                // Notify the organizer that registration payment was received
                var organizerUserId = await _context.OrganizedTrips
                    .Where(t => t.Id == organizedTrip.Id)
                    .Join(_context.OrganizerProfiles,
                        t => t.OrganizerId,
                        op => op.Id,
                        (t, op) => op.UserId)
                    .FirstOrDefaultAsync();

                if (organizerUserId > 0)
                {
                    await _notificationService.CreateNotificationAsync(
                        organizerUserId,
                        "Registration payment received",
                        $"A tourist paid {payment.Amount:C} for '{organizedTrip.Title}'. The registration is confirmed.");
                }

                return new PaymentResultDto
                {
                    Success = true,
                    Error   = PaymentError.None,
                    Message = "Registration payment deposited and held in escrow. Registration confirmed.",
                    Data    = ToResponseDto(payment)
                };
            }
            else
            {
                payment.Status        = PaymentStatus.Failed;
                payment.FailureReason = gatewayResult.FailureReason ?? "Payment declined.";
                await _context.SaveChangesAsync();
                if (tx is not null) await tx.CommitAsync();

                _logger.LogWarning(
                    "Registration payment failed for Registration {RegistrationId}. Reason: {Reason}",
                    registrationId, payment.FailureReason);

                return new PaymentResultDto
                {
                    Success = false,
                    Error   = PaymentError.PaymentFailed,
                    Message = payment.FailureReason,
                    Data    = ToResponseDto(payment)
                };
            }
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex,
                "Unique constraint on registration payment for Registration {RegistrationId}.", registrationId);
            return Failure(PaymentError.AlreadyPaid,
                "This registration has already been paid and confirmed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing registration payment for Registration {RegistrationId}.", registrationId);
            return Failure(PaymentError.ServerError,
                "An unexpected error occurred. Please retry.");
        }
    }

    // ── Escrow release ───────────────────────────────────────────────────────

    public async Task<PaymentResultDto> ReleaseEscrowAsync(int touristUserId, int paymentId)
    {
        var payment = await _context.Payments
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            return Failure(PaymentError.PaymentNotFound, "Payment not found.");
        if (payment.TouristUserId != touristUserId)
            return Failure(PaymentError.Unauthorized,
                "You are not authorized to release this escrow.");

        // Must be Held or PartiallyRefunded (remainder is still held)
        if (payment.Status != PaymentStatus.Held
            && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            if (payment.Status == PaymentStatus.Released
                || payment.Status == PaymentStatus.Refunded)
                return Failure(PaymentError.EscrowAlreadySettled,
                    "This escrow has already been settled.");
            return Failure(PaymentError.EscrowAlreadySettled,
                "Only held payments can be released.");
        }

        // Verify the associated trip has completed
        var tripCompleted = await IsTripCompletedAsync(payment);
        if (!tripCompleted)
            return Failure(PaymentError.TripNotCompleted,
                "Escrow can only be released after the associated trip has completed.");

        // Block release if an open dispute exists for this trip
        var hasOpenDispute = await HasOpenDisputeAsync(payment);
        if (hasOpenDispute)
            return Failure(PaymentError.DisputeBlocksRelease,
                "Release is blocked by an open dispute. Please resolve the dispute first.");

        var now = DateTime.UtcNow;

        if (payment.Milestones.Count > 0)
        {
            foreach (var milestone in payment.Milestones
                         .Where(m => m.Status == PaymentMilestoneStatus.Held))
            {
                milestone.Status     = PaymentMilestoneStatus.Released;
                milestone.ReleasedAt = now;
            }
            SyncAggregateFromMilestones(payment, now);
        }
        else
        {
            payment.Status           = PaymentStatus.Released;
            payment.EscrowReleasedAt = now;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Escrow released for Payment {PaymentId} by tourist {TouristUserId}.",
            paymentId, touristUserId);

        // Notify the guide/organizer that escrow was released
        var recipientUserId = await GetPaymentRecipientUserIdAsync(payment);
        if (recipientUserId > 0)
        {
            await _notificationService.CreateNotificationAsync(
                recipientUserId,
                "Escrow released",
                $"The tourist has released the escrow for payment of {payment.Amount:C}.");
        }

        return new PaymentResultDto
        {
            Success = true,
            Error   = PaymentError.None,
            Message = "Escrow released successfully.",
            Data    = ToResponseDto(payment)
        };
    }

    public async Task<PaymentResultDto> ReleaseMilestoneAsync(
        int touristUserId,
        int paymentId,
        int milestoneId)
    {
        var payment = await _context.Payments
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            return Failure(PaymentError.PaymentNotFound, "Payment not found.");
        if (payment.TouristUserId != touristUserId)
            return Failure(PaymentError.Unauthorized,
                "You are not authorized to release this escrow.");

        var milestone = payment.Milestones.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone is null)
            return Failure(PaymentError.MilestoneNotFound,
                "Milestone not found on this payment.");

        if (payment.Status != PaymentStatus.Held
            && payment.Status != PaymentStatus.PartiallyRefunded)
            return Failure(PaymentError.EscrowAlreadySettled,
                "Only held payments can have milestones released.");

        if (milestone.Status != PaymentMilestoneStatus.Held)
            return Failure(PaymentError.MilestoneAlreadySettled,
                $"Milestone {milestone.Index} is already {milestone.Status}.");

        if (!await IsTripCompletedAsync(payment))
            return Failure(PaymentError.TripNotCompleted,
                "Milestones can only be released after the associated trip has completed.");

        if (await HasOpenDisputeAsync(payment))
            return Failure(PaymentError.DisputeBlocksRelease,
                "Release is blocked by an open dispute. Please resolve the dispute first.");

        var now = DateTime.UtcNow;
        milestone.Status     = PaymentMilestoneStatus.Released;
        milestone.ReleasedAt = now;
        SyncAggregateFromMilestones(payment, now);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Milestone {MilestoneId} released for Payment {PaymentId} by tourist {TouristUserId}.",
            milestoneId, paymentId, touristUserId);

        // Notify the guide/organizer that a milestone was released
        var recipientUserId = await GetPaymentRecipientUserIdAsync(payment);
        if (recipientUserId > 0)
        {
            await _notificationService.CreateNotificationAsync(
                recipientUserId,
                "Milestone released",
                $"Milestone {milestone.Index} ({milestone.Label}) for {payment.Amount:C} has been released.");
        }

        return new PaymentResultDto
        {
            Success = true,
            Error   = PaymentError.None,
            Message = $"Milestone {milestone.Index} ({milestone.Label}) released.",
            Data    = ToResponseDto(payment)
        };
    }

    /// <summary>
    /// Keeps the parent payment status consistent with its milestones: the escrow is
    /// settled once no milestone is still held, and stays Held while any is unspent.
    /// </summary>
    private static void SyncAggregateFromMilestones(Payment payment, DateTime now)
    {
        if (payment.Milestones.Count == 0)
            return;

        if (payment.Milestones.Any(m => m.Status == PaymentMilestoneStatus.Held))
            return;

        var anyReleased = payment.Milestones.Any(m => m.Status == PaymentMilestoneStatus.Released);

        payment.Status = anyReleased ? PaymentStatus.Released : PaymentStatus.Refunded;

        if (anyReleased && payment.EscrowReleasedAt is null)
        {
            var releasedDates = payment.Milestones
                .Where(m => m.ReleasedAt.HasValue)
                .Select(m => m.ReleasedAt!.Value)
                .ToList();

            payment.EscrowReleasedAt = releasedDates.Count > 0 ? releasedDates.Max() : now;
        }
    }

    // ── Refund (admin) ───────────────────────────────────────────────────────

    public async Task<PaymentResultDto> RefundAsync(
        int adminUserId,
        int paymentId,
        decimal? amount,
        string? reason)
    {
        // The controller enforces the Admin role; the service still enforces every
        // payment-state, dispute and amount rule so a role cannot bypass them.
        var payment = await _context.Payments
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            return Failure(PaymentError.PaymentNotFound, "Payment not found.");

        if (amount.HasValue && amount.Value <= 0m)
            return Failure(PaymentError.InvalidRequest,
                "Refund amount must be greater than zero. Omit Amount to refund the full remaining balance.");

        // Only Held or PartiallyRefunded payments are refundable
        if (payment.Status != PaymentStatus.Held
            && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            if (payment.Status == PaymentStatus.Released)
                return Failure(PaymentError.NotRefundable,
                    "Escrow has already been released; it cannot be refunded.");
            if (payment.Status == PaymentStatus.Refunded)
                return Failure(PaymentError.EscrowAlreadySettled,
                    "This payment has already been fully refunded.");
            return Failure(PaymentError.NotRefundable,
                "Only held or partially-refunded payments can be refunded.");
        }

        // A refund may not silently overstep an unresolved dispute on this trip.
        if (await HasOpenDisputeAsync(payment))
            return Failure(PaymentError.DisputeResolutionRequired,
                "An open dispute exists for this trip. Resolve the dispute before refunding.");

        // Amounts already paid out through released milestones are no longer refundable.
        var alreadyRefunded = payment.RefundedAmount ?? 0m;
        var releasedTotal   = ReleasedMilestoneTotal(payment);
        var maxRefundable   = payment.Amount - alreadyRefunded - releasedTotal;

        if (maxRefundable <= 0)
            return Failure(PaymentError.EscrowAlreadySettled,
                "There is no remaining refundable balance.");

        // A null amount means "refund the full remaining balance".
        var refundAmount = amount ?? maxRefundable;

        if (refundAmount > maxRefundable)
            return Failure(PaymentError.RefundExceedsHeldAmount,
                $"Requested refund ({refundAmount:F2}) exceeds the remaining refundable balance ({maxRefundable:F2}).");

        payment.RefundedAmount = alreadyRefunded + refundAmount;
        payment.RefundedAt     = DateTime.UtcNow;
        payment.RefundReason   = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        // If the entire held amount is now refunded, mark as fully Refunded; otherwise PartiallyRefunded
        payment.Status = payment.RefundedAmount >= payment.Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        if (payment.Status == PaymentStatus.Refunded)
        {
            var now = DateTime.UtcNow;
            foreach (var milestone in payment.Milestones
                         .Where(m => m.Status == PaymentMilestoneStatus.Held))
            {
                milestone.Status     = PaymentMilestoneStatus.Refunded;
                milestone.RefundedAt = now;
            }
        }
        else if (payment.RefundedAmount + releasedTotal >= payment.Amount)
        {
            // Part of the escrow went to the organizer and the remainder is now refunded:
            // nothing is held any more, so no milestone may stay Held.
            var now = DateTime.UtcNow;
            foreach (var milestone in payment.Milestones
                         .Where(m => m.Status == PaymentMilestoneStatus.Held))
            {
                milestone.Status     = PaymentMilestoneStatus.Refunded;
                milestone.RefundedAt = now;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Refund of {Amount} issued for Payment {PaymentId} by admin {AdminUserId}. New status: {Status}.",
            refundAmount, paymentId, adminUserId, payment.Status);

        // Notify the tourist that a refund was issued
        if (payment.TouristUserId > 0)
        {
            await _notificationService.CreateNotificationAsync(
                payment.TouristUserId,
                "Refund issued",
                $"A refund of {refundAmount:C} has been issued for your payment.");
        }

        return new PaymentResultDto
        {
            Success = true,
            Error   = PaymentError.None,
            Message = payment.Status == PaymentStatus.Refunded
                ? "Full refund issued successfully."
                : $"Partial refund of {refundAmount:F2} {payment.Currency} issued.",
            Data    = ToResponseDto(payment)
        };
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<PaymentListResultDto> GetMyPaymentsAsync(int touristUserId)
    {
        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.TouristUserId == touristUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        await AttachMilestonesAsync(payments);

        return new PaymentListResultDto
        {
            Success    = true,
            Message    = payments.Count == 0
                ? "No payment records found."
                : $"Showing {payments.Count} payment record(s).",
            Data       = payments.Select(ToResponseDto).ToList(),
            TotalCount = payments.Count
        };
    }

    public async Task<PaymentListResultDto> GetTripPaymentsAsync(int organizerUserId, int organizedTripId)
    {
        // Verify the organizer owns the trip
        var organizer = await _context.OrganizerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(op => op.UserId == organizerUserId);

        if (organizer is null)
            return new PaymentListResultDto
            {
                Success = false,
                Error   = PaymentError.OrganizerProfileNotFound,
                Message = "Organizer profile not found."
            };

        var trip = await _context.OrganizedTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == organizedTripId);

        if (trip is null)
            return new PaymentListResultDto
            {
                Success = false,
                Error   = PaymentError.OrganizedTripNotFound,
                Message = "Organized trip not found."
            };

        if (trip.OrganizerId != organizer.Id)
            return new PaymentListResultDto
            {
                Success = false,
                Error   = PaymentError.Unauthorized,
                Message = "You can only view payments for trips owned by your organizer profile."
            };

        // Join Payments → Registrations scoped to this trip
        var payments = await (
            from payment in _context.Payments.AsNoTracking()
            join registration in _context.Registrations.AsNoTracking()
                on payment.RegistrationId equals registration.Id
            where registration.OrganizedTripId == organizedTripId
            orderby payment.CreatedAt descending
            select payment
        ).ToListAsync();

        await AttachMilestonesAsync(payments);

        return new PaymentListResultDto
        {
            Success    = true,
            Message    = payments.Count == 0
                ? "No payment records for this trip."
                : $"Showing {payments.Count} payment record(s).",
            Data       = payments.Select(ToResponseDto).ToList(),
            TotalCount = payments.Count
        };
    }

    public async Task<PaymentListResultDto> GetPaymentsByUserAsync(int targetUserId)
    {
        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.TouristUserId == targetUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        await AttachMilestonesAsync(payments);

        return new PaymentListResultDto
        {
            Success    = true,
            Message    = payments.Count == 0
                ? "No payment records found for this user."
                : $"Showing {payments.Count} payment record(s).",
            Data       = payments.Select(ToResponseDto).ToList(),
            TotalCount = payments.Count
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the trip associated with the payment has reached a
    /// terminal completed state. Covers both TripRequest (guide proposal)
    /// and OrganizedTrip (registration) flows.
    /// </summary>
    private async Task<bool> IsTripCompletedAsync(Payment payment)
    {
        if (payment.TargetType == PaymentTargetType.Proposal && payment.ProposalId.HasValue)
        {
            var proposal = await _context.Proposals
                .AsNoTracking()
                .Include(p => p.TripRequest)
                .FirstOrDefaultAsync(p => p.Id == payment.ProposalId.Value);

            return proposal?.TripRequest?.Status == TripRequestStatus.Completed;
        }

        if (payment.TargetType == PaymentTargetType.Registration && payment.RegistrationId.HasValue)
        {
            var registration = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payment.RegistrationId.Value);

            if (registration is null) return false;

            var trip = await _context.OrganizedTrips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == registration.OrganizedTripId);

            return trip?.Status == OrganizedTripStatus.Completed;
        }

        return false;
    }

    /// <summary>
    /// Returns true when there is at least one Open dispute against the trip
    /// associated with the payment.
    /// </summary>
    private async Task<bool> HasOpenDisputeAsync(Payment payment)
    {
        if (payment.TargetType == PaymentTargetType.Proposal && payment.ProposalId.HasValue)
        {
            var proposal = await _context.Proposals
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == payment.ProposalId.Value);

            if (proposal is null) return false;

            return await _context.Disputes.AnyAsync(d =>
                d.TripType == DisputeRelatedTripType.TripRequest &&
                d.TripId   == proposal.TripRequestId &&
                d.Status   == DisputeStatus.Open);
        }

        if (payment.TargetType == PaymentTargetType.Registration && payment.RegistrationId.HasValue)
        {
            var registration = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payment.RegistrationId.Value);

            if (registration is null) return false;

            return await _context.Disputes.AnyAsync(d =>
                d.TripType == DisputeRelatedTripType.OrganizedTrip &&
                d.TripId   == registration.OrganizedTripId &&
                d.Status   == DisputeStatus.Open);
        }

return false;
        }

        /// <summary>
        /// Returns the UserId of the payment recipient (guide for proposals, organizer for registrations).
        /// </summary>
        private async Task<int> GetPaymentRecipientUserIdAsync(Payment payment)
        {
            if (payment.TargetType == PaymentTargetType.Proposal && payment.ProposalId.HasValue)
            {
                return await _context.Proposals
                    .AsNoTracking()
                    .Where(p => p.Id == payment.ProposalId.Value)
                    .Select(p => p.GuideId)
                    .FirstOrDefaultAsync();
            }

            if (payment.TargetType == PaymentTargetType.Registration && payment.RegistrationId.HasValue)
            {
                var registration = await _context.Registrations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == payment.RegistrationId.Value);

                if (registration is null) return 0;

                return await _context.OrganizedTrips
                    .Where(t => t.Id == registration.OrganizedTripId)
                    .Join(_context.OrganizerProfiles,
                        t => t.OrganizerId,
                        op => op.Id,
                        (t, op) => op.UserId)
                    .FirstOrDefaultAsync();
            }

            return 0;
        }
    }
