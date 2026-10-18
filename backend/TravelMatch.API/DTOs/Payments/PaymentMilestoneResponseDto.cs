using TravelMatch.API.Models;

namespace TravelMatch.API.DTOs.Payments;

public class PaymentMilestoneResponseDto
{
    public int MilestoneId { get; set; }

    public int Index { get; set; }

    public string Label { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public PaymentMilestoneStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? HeldAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public DateTime? RefundedAt { get; set; }
}
