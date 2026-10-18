namespace TravelMatch.API.DTOs.Proposals;

/// <summary>
/// Proposal as seen by the guide who owns it.
/// </summary>
public class GuideProposalDto
{
    public int Id { get; set; }

    public int TripRequestId { get; set; }

    public string TripRequestDestination { get; set; } = string.Empty;

    public DateOnly TripRequestStartDate { get; set; }

    public DateOnly TripRequestEndDate { get; set; }

    public decimal ProposedPrice { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Availability { get; set; } = string.Empty;

    public string Inclusions { get; set; } = string.Empty;

    public string Exclusions { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int ItineraryDayCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
