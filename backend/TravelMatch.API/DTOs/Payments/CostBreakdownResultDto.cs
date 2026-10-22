namespace TravelMatch.API.DTOs.Payments;

/// <summary>Envelope returned by cost-breakdown endpoints.</summary>
public class CostBreakdownResultDto
{
    public bool Success { get; init; }

    public CostBreakdownError Error { get; init; } = CostBreakdownError.None;

    public string Message { get; init; } = string.Empty;

    public CostBreakdownDto? Data { get; init; }
}

public enum CostBreakdownError
{
    None,
    InvalidRequest,
    ProposalNotFound,
    OrganizedTripNotFound,
    RegistrationNotFound,
    Unauthorized,
    ServerError
}
