namespace TravelMatch.API.DTOs.GuideApplications;

public class GuideApplicationDto
{
    public int Id { get; set; }

    public int GuideId { get; set; }

    public string GuideName { get; set; } = string.Empty;

    public int TripId { get; set; }

    public string TripTitle { get; set; } = string.Empty;

    public decimal ProposedPrice { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
