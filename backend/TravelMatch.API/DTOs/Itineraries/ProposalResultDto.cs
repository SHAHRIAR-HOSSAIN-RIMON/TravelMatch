namespace TravelMatch.API.DTOs.Itineraries;

public class ProposalResultDto
{
    public bool Success { get; set; }

    public ItineraryError Error { get; set; } = ItineraryError.None;

    public string Message { get; set; } = string.Empty;

    public ProposalDto? Data { get; set; }
}
