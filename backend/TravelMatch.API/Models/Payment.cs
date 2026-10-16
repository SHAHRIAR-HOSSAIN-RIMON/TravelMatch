namespace TravelMatch.API.Models;

public class Payment
{
    public int Id { get; set; }

    public int TouristUserId { get; set; }

    public User TouristUser { get; set; } = null!;

    public PaymentTargetType TargetType { get; set; }

    public int? ProposalId { get; set; }

    public Proposal? Proposal { get; set; }

    public int? RegistrationId { get; set; }

    public Registration? Registration { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string TransactionReference { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the gateway call succeeds and funds enter simulated escrow.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Set when the held escrow is released to the guide/organizer.</summary>
    public DateTime? EscrowReleasedAt { get; set; }

    /// <summary>Amount returned to the tourist (full or partial refund).</summary>
    public decimal? RefundedAmount { get; set; }

    /// <summary>Set when a refund is issued.</summary>
    public DateTime? RefundedAt { get; set; }

    /// <summary>Admin-supplied explanation for the refund, kept separate from gateway failures.</summary>
    public string? RefundReason { get; set; }

    public List<PaymentMilestone> Milestones { get; set; } = new();
}
