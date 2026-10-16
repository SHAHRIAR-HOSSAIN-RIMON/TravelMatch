namespace TravelMatch.API.Models;

/// <summary>
/// A labelled slice of a <see cref="Payment"/> that is held and released
/// independently inside simulated escrow. No real money moves.
/// </summary>
public class PaymentMilestone
{
    public int Id { get; set; }

    public int PaymentId { get; set; }

    public Payment Payment { get; set; } = null!;

    /// <summary>One-based ordering position, unique per payment.</summary>
    public int Index { get; set; }

    public string Label { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public PaymentMilestoneStatus Status { get; set; } = PaymentMilestoneStatus.Held;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the milestone entered escrow with its parent deposit.</summary>
    public DateTime? HeldAt { get; set; }

    /// <summary>Set when this milestone was released to the guide/organizer.</summary>
    public DateTime? ReleasedAt { get; set; }

    /// <summary>Set when this milestone was returned to the tourist.</summary>
    public DateTime? RefundedAt { get; set; }
}
