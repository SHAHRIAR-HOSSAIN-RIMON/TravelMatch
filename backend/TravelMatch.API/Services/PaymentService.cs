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

    public PaymentService(
        ApplicationDbContext context,
        ISandboxPaymentGateway paymentGateway,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    public async Task<PaymentResultDto> PayForProposalAsync(
        int touristUserId,
        int proposalId,
        PayProposalRequestDto? request = null)
    {
        if (proposalId <= 0)
        {
            return new PaymentResultDto
            {
                Success = false,
                Error = PaymentError.InvalidRequest,
                Message = "Invalid proposal identifier."
            };
        }

        try
        {
            var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

            await using var _ = transaction;

            var proposal = await _context.Proposals
                .Include(p => p.TripRequest)
                .FirstOrDefaultAsync(p => p.Id == proposalId);

            if (proposal is null)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.ProposalNotFound,
                    Message = "Proposal not found."
                };
            }

            if (proposal.TripRequest is null)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.TripRequestNotFound,
                    Message = "Associated trip request not found."
                };
            }

            // Authorization check: only the tourist who owns the trip request can pay
            if (proposal.TripRequest.TouristId != touristUserId)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.Unauthorized,
                    Message = "You are not authorized to pay for this proposal."
                };
            }

            // Proposal must be accepted to proceed to payment
            if (proposal.Status != ProposalStatus.Accepted)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.InvalidProposalStatus,
                    Message = "Payments can only be made for accepted proposals."
                };
            }

            // Check if there is already a successful payment for this proposal (application-level check)
            var existingSuccessfulPayment = await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ProposalId == proposalId &&
                    p.Status == PaymentStatus.Completed);

            if (existingSuccessfulPayment is not null)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.AlreadyPaid,
                    Message = "Payment has already been completed for this proposal.",
                    Data = ToResponseDto(existingSuccessfulPayment)
                };
            }

            // Derive payment amount authoritatively from proposal.ProposedPrice
            var paymentAmount = proposal.ProposedPrice;
            if (paymentAmount <= 0)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.InvalidRequest,
                    Message = "Proposal price must be greater than zero."
                };
            }

            // Create pending payment record
            var payment = new Payment
            {
                TouristUserId = touristUserId,
                TargetType = PaymentTargetType.Proposal,
                ProposalId = proposalId,
                Amount = paymentAmount,
                Currency = "USD",
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Simulate payment processing via decoupled sandbox payment component
            var gatewayResult = await _paymentGateway.ProcessPaymentAsync(
                payment.Amount,
                payment.Currency,
                request?.PaymentMethod);

            payment.TransactionReference = gatewayResult.TransactionReference;

            if (gatewayResult.IsSuccess)
            {
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;
                payment.FailureReason = null;

                await _context.SaveChangesAsync();

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                }

                _logger.LogInformation(
                    "Proposal payment {PaymentId} succeeded for Proposal {ProposalId} by tourist {TouristUserId}.",
                    payment.Id,
                    proposalId,
                    touristUserId);

                return new PaymentResultDto
                {
                    Success = true,
                    Error = PaymentError.None,
                    Message = "Payment completed successfully.",
                    Data = ToResponseDto(payment)
                };
            }
            else
            {
                // Failed payment: remain retryable, mark payment as Failed, leave proposal state intact
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = gatewayResult.FailureReason ?? "Payment declined.";

                await _context.SaveChangesAsync();

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                }

                _logger.LogWarning(
                    "Proposal payment failed for Proposal {ProposalId} by tourist {TouristUserId}. Reason: {Reason}",
                    proposalId,
                    touristUserId,
                    payment.FailureReason);

                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.PaymentFailed,
                    Message = payment.FailureReason,
                    Data = ToResponseDto(payment)
                };
            }
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                ex,
                "Unique constraint violation detected on proposal payment completion for Proposal {ProposalId}.",
                proposalId);

            return new PaymentResultDto
            {
                Success = false,
                Error = PaymentError.AlreadyPaid,
                Message = "Payment has already been completed for this proposal."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing proposal payment for proposal {ProposalId} by tourist {TouristUserId}.",
                proposalId,
                touristUserId);

            return new PaymentResultDto
            {
                Success = false,
                Error = PaymentError.ServerError,
                Message = "An unexpected error occurred while processing the payment. Please retry."
            };
        }
    }

    public async Task<PaymentResultDto> PayForRegistrationAsync(
        int touristUserId,
        int registrationId,
        PayRegistrationRequestDto? request = null)
    {
        if (registrationId <= 0)
        {
            return new PaymentResultDto
            {
                Success = false,
                Error = PaymentError.InvalidRequest,
                Message = "Invalid registration identifier."
            };
        }

        try
        {
            var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

            await using var _ = transaction;

            var touristProfile = await _context.TouristProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(tp => tp.UserId == touristUserId);

            if (touristProfile is null)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.TouristProfileNotFound,
                    Message = "Tourist profile not found for authenticated user."
                };
            }

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration is null)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.RegistrationNotFound,
                    Message = "Registration not found."
                };
            }

            // Authorization check: registration must belong to the authenticated tourist's profile
            if (registration.TouristId != touristProfile.Id)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.Unauthorized,
                    Message = "You are not authorized to pay for this registration."
                };
            }

            // Registration must be PendingPayment to proceed (cannot pay cancelled or already confirmed)
            if (registration.Status == RegistrationStatus.Confirmed)
            {
                var existingConfirmedPayment = await _context.Payments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p =>
                        p.RegistrationId == registrationId &&
                        p.Status == PaymentStatus.Completed);

                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.AlreadyPaid,
                    Message = "This registration has already been paid and confirmed.",
                    Data = existingConfirmedPayment is not null ? ToResponseDto(existingConfirmedPayment) : null
                };
            }

            if (registration.Status == RegistrationStatus.Cancelled)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.InvalidRegistrationStatus,
                    Message = "Cannot pay for a cancelled registration."
                };
            }

            // Concurrency-safe OrganizedTrip row locking on relational/PostgreSQL database:
            // Locks the specific OrganizedTrip row with FOR UPDATE to prevent race conditions during capacity checking and confirmation.
            OrganizedTrip? organizedTrip;
            if (_context.Database.IsRelational())
            {
                organizedTrip = await _context.OrganizedTrips
                    .FromSqlInterpolated($"SELECT * FROM \"OrganizedTrips\" WHERE \"Id\" = {registration.OrganizedTripId} FOR UPDATE")
                    .FirstOrDefaultAsync();
            }
            else
            {
                organizedTrip = await _context.OrganizedTrips
                    .FirstOrDefaultAsync(t => t.Id == registration.OrganizedTripId);
            }

            if (organizedTrip is null)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.OrganizedTripNotFound,
                    Message = "Associated organized trip not found."
                };
            }

            if (organizedTrip.Status != OrganizedTripStatus.RegistrationOpen)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.TripNotAvailableForRegistration,
                    Message = "The organized trip is not open for registration."
                };
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (organizedTrip.RegistrationDeadline < today)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.RegistrationDeadlinePassed,
                    Message = "The registration deadline for this trip has passed."
                };
            }

            // Check capacity: count confirmed registrations under row-level lock
            var currentConfirmedCount = await _context.Registrations
                .CountAsync(r =>
                    r.OrganizedTripId == organizedTrip.Id &&
                    r.Status == RegistrationStatus.Confirmed &&
                    r.Id != registrationId);

            if (currentConfirmedCount >= organizedTrip.MaxParticipants)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.TripCapacityExceeded,
                    Message = "This trip has reached its maximum participant capacity."
                };
            }

            // Authoritative amount from organizedTrip.PricePerPerson
            var paymentAmount = organizedTrip.PricePerPerson;
            if (paymentAmount <= 0)
            {
                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.InvalidRequest,
                    Message = "Trip price per person must be greater than zero."
                };
            }

            // Create pending payment record
            var payment = new Payment
            {
                TouristUserId = touristUserId,
                TargetType = PaymentTargetType.Registration,
                RegistrationId = registrationId,
                Amount = paymentAmount,
                Currency = "USD",
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Simulate payment processing via decoupled sandbox payment component
            var gatewayResult = await _paymentGateway.ProcessPaymentAsync(
                payment.Amount,
                payment.Currency,
                request?.PaymentMethod);

            payment.TransactionReference = gatewayResult.TransactionReference;

            if (gatewayResult.IsSuccess)
            {
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;
                payment.FailureReason = null;

                // Confirm registration ONLY upon successful payment
                registration.Status = RegistrationStatus.Confirmed;

                await _context.SaveChangesAsync();

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                }

                _logger.LogInformation(
                    "Registration payment {PaymentId} succeeded for Registration {RegistrationId} by tourist {TouristUserId}.",
                    payment.Id,
                    registrationId,
                    touristUserId);

                return new PaymentResultDto
                {
                    Success = true,
                    Error = PaymentError.None,
                    Message = "Registration payment completed and registration confirmed.",
                    Data = ToResponseDto(payment)
                };
            }
            else
            {
                // Failed payment: remain retryable, mark payment as Failed, registration remains PendingPayment
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = gatewayResult.FailureReason ?? "Payment declined.";

                await _context.SaveChangesAsync();

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                }

                _logger.LogWarning(
                    "Registration payment failed for Registration {RegistrationId} by tourist {TouristUserId}. Reason: {Reason}",
                    registrationId,
                    touristUserId,
                    payment.FailureReason);

                return new PaymentResultDto
                {
                    Success = false,
                    Error = PaymentError.PaymentFailed,
                    Message = payment.FailureReason,
                    Data = ToResponseDto(payment)
                };
            }
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                ex,
                "Unique constraint violation detected on registration payment completion for Registration {RegistrationId}.",
                registrationId);

            return new PaymentResultDto
            {
                Success = false,
                Error = PaymentError.AlreadyPaid,
                Message = "This registration has already been paid and confirmed."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing registration payment for registration {RegistrationId} by tourist {TouristUserId}.",
                registrationId,
                touristUserId);

            return new PaymentResultDto
            {
                Success = false,
                Error = PaymentError.ServerError,
                Message = "An unexpected error occurred while processing the payment. Please retry."
            };
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return true;
        }

        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_Payments_ProposalId_Completed_Unique", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_Payments_RegistrationId_Completed_Unique", StringComparison.OrdinalIgnoreCase);
    }

    private static PaymentResponseDto ToResponseDto(Payment payment)
    {
        return new PaymentResponseDto
        {
            PaymentId = payment.Id,
            TouristUserId = payment.TouristUserId,
            TargetType = payment.TargetType,
            ProposalId = payment.ProposalId,
            RegistrationId = payment.RegistrationId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status,
            TransactionReference = payment.TransactionReference,
            FailureReason = payment.FailureReason,
            CreatedAt = payment.CreatedAt,
            CompletedAt = payment.CompletedAt
        };
    }
}
