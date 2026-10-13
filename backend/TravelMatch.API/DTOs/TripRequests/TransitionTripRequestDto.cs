namespace TravelMatch.API.DTOs.TripRequests;

public class TransitionTripRequestDto
{
    public int ProposalId { get; set; }

    public decimal AcceptedPrice { get; set; }

    public string? CancellationReason { get; set; }
}