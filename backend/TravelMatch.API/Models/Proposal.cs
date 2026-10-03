namespace TravelMatch.API.Models;

public class Proposal
{
    public int Id { get; set; }

    public int GuideId { get; set; }

    public int TripRequestId { get; set; }

    public decimal Price { get; set; }

    public decimal? EstimatedExpenses { get; set; }

    public string Availability { get; set; } = string.Empty;

    public string Inclusions { get; set; } = string.Empty;

    public string? Exclusions { get; set; }

    public string? AdditionalNotes { get; set; }

    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}