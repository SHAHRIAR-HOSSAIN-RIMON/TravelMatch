namespace TravelMatch.API.DTOs.Payments;

public class PayProposalRequestDto
{
    public string? PaymentMethod { get; set; }

    /// <summary>Optional escrow split. When supplied the amounts must sum to the payment amount.</summary>
    public List<CreatePaymentMilestoneDto>? Milestones { get; set; }
}

public class PayRegistrationRequestDto
{
    public string? PaymentMethod { get; set; }

    /// <summary>Optional escrow split. When supplied the amounts must sum to the payment amount.</summary>
    public List<CreatePaymentMilestoneDto>? Milestones { get; set; }
}

/// <summary>Body for POST /api/payments/{id}/refund.</summary>
public class RefundRequestDto
{
    /// <summary>
    /// Amount to refund. Must be > 0 and ≤ remaining held amount.
    /// Omit or set to null to request a full refund of the held amount.
    /// </summary>
    public decimal? Amount { get; set; }

    public string? Reason { get; set; }
}
