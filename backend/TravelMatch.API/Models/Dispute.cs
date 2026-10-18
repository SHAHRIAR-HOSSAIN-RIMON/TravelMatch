namespace TravelMatch.API.Models;

public class Dispute
{
    public int Id { get; set; }
    public int ReportedByUserId { get; set; }
    public User ReportedByUser { get; set; } = null!;
    public DisputeRelatedTripType TripType { get; set; }
    public int TripId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Admin note recorded when the dispute was resolved.</summary>
    public string? ResolutionNote { get; set; }

    public int? ResolvedByUserId { get; set; }

    public User? ResolvedByUser { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
