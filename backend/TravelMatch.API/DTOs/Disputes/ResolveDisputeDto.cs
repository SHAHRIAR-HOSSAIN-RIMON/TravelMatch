namespace TravelMatch.API.DTOs.Disputes;

/// <summary>Body for POST /api/disputes/{disputeId}/resolve.</summary>
public class ResolveDisputeDto
{
    /// <summary>Admin's account of how the dispute was settled. Required.</summary>
    public string? ResolutionNote { get; set; }
}
