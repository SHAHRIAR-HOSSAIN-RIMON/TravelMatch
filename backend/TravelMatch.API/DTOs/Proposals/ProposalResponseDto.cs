namespace TravelMatch.API.DTOs.Proposals;

public class ProposalResponseDto
{
    public int Id { get; set; }

    public int GuideId { get; set; }

    public int TripRequestId { get; set; }

    public string Destination { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal TripRequestBudget { get; set; }

    public decimal Price { get; set; }

    public decimal? EstimatedExpenses { get; set; }

    public string Availability { get; set; } = string.Empty;

    public string Inclusions { get; set; } = string.Empty;

    public string? Exclusions { get; set; }

    public string? AdditionalNotes { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; }
}