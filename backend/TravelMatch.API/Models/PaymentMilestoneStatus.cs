namespace TravelMatch.API.Models;

public enum PaymentMilestoneStatus
{
    /// <summary>Milestone amount is funded and held in simulated escrow.</summary>
    Held,

    /// <summary>Milestone amount released to the guide/organizer.</summary>
    Released,

    /// <summary>Milestone amount returned to the tourist.</summary>
    Refunded
}
