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

    public DateTime? CompletedAt { get; set; }
}
