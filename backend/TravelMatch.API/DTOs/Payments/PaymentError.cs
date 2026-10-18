namespace TravelMatch.API.DTOs.Payments;

public enum PaymentError
{
    None,
    InvalidRequest,
    Unauthorized,
    TouristProfileNotFound,
    OrganizerProfileNotFound,
    ProposalNotFound,
    TripRequestNotFound,
    RegistrationNotFound,
    OrganizedTripNotFound,
    PaymentNotFound,
    InvalidProposalStatus,
    InvalidRegistrationStatus,
    AlreadyPaid,
    PaymentFailed,
    TripCapacityExceeded,
    RegistrationDeadlinePassed,
    TripNotAvailableForRegistration,
    /// <summary>Escrow cannot be released because the trip has not completed yet.</summary>
    TripNotCompleted,
    /// <summary>Escrow was already released or refunded; operation is a duplicate.</summary>
    EscrowAlreadySettled,
    /// <summary>Release is blocked by an open dispute on this trip.</summary>
    DisputeBlocksRelease,
    /// <summary>Requested refund amount exceeds the held amount.</summary>
    RefundExceedsHeldAmount,
    /// <summary>The payment is not in a state that can be refunded (not Held/PartiallyRefunded).</summary>
    NotRefundable,
    /// <summary>An open dispute on the associated trip must be resolved by an admin before refunding.</summary>
    DisputeResolutionRequired,
    /// <summary>A milestone amount is missing, non-positive, or the index is duplicated.</summary>
    InvalidMilestone,
    /// <summary>Milestone amounts do not add up to the payment amount.</summary>
    MilestoneAmountMismatch,
    /// <summary>Milestone was already released or refunded.</summary>
    MilestoneAlreadySettled,
    /// <summary>Milestone does not exist on this payment.</summary>
    MilestoneNotFound,
    ServerError
}
