namespace TravelMatch.API.DTOs.Itineraries;

public class ProposalDto
{
    public int Id { get; set; }

    public int GuideId { get; set; }

    public string GuideName { get; set; } = string.Empty;

    public int TripRequestId { get; set; }

    public decimal ProposedPrice { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyList<ItineraryDayDto> ItineraryDays { get; set; } = Array.Empty<ItineraryDayDto>();
}
