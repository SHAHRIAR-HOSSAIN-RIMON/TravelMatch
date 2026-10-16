namespace TravelMatch.API.DTOs.Payments;

/// <summary>Declares one escrow slice at deposit time.</summary>
public class CreatePaymentMilestoneDto
{
    /// <summary>One-based ordering position, unique within the payment.</summary>
    public int Index { get; set; }

    public string Label { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
