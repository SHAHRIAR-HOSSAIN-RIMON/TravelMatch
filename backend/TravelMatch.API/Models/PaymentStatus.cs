namespace TravelMatch.API.Models;

public enum PaymentStatus
{
    /// <summary>Payment record created; gateway call not yet made.</summary>
    Pending,

    /// <summary>Gateway succeeded; funds are held in simulated escrow.</summary>
    Held,

    /// <summary>Escrow released to the guide / organizer after trip completion.</summary>
    Released,

    /// <summary>Gateway declined; tourist may retry.</summary>
    Failed,

    /// <summary>Full refund issued back to the tourist.</summary>
    Refunded,

    /// <summary>Partial refund issued; remainder retained.</summary>
    PartiallyRefunded,

    /// <summary>Legacy terminal state kept for backward-compatibility with early payments.</summary>
    Completed
}
