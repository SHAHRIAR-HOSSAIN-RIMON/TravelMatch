using TravelMatch.API.Models;

namespace TravelMatch.API.DTOs.Payments;

public class PaymentResponseDto
{
    public int PaymentId { get; set; }

    public int TouristUserId { get; set; }

    public PaymentTargetType TargetType { get; set; }

    public int? ProposalId { get; set; }

    public int? RegistrationId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public PaymentStatus Status { get; set; }

    public string TransactionReference { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the gateway succeeded and funds entered simulated escrow.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>When the held escrow was released to the guide/organizer.</summary>
    public DateTime? EscrowReleasedAt { get; set; }

    /// <summary>Amount returned to the tourist (full or partial).</summary>
    public decimal? RefundedAmount { get; set; }

    /// <summary>When a refund was issued.</summary>
    public DateTime? RefundedAt { get; set; }

    /// <summary>Admin-supplied explanation for the refund.</summary>
    public string? RefundReason { get; set; }

    /// <summary>Empty when the payment was deposited without milestone allocation.</summary>
    public List<PaymentMilestoneResponseDto> Milestones { get; set; } = new();
}
