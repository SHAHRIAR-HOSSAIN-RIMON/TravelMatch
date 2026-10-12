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

    public DateTime? CompletedAt { get; set; }
}
