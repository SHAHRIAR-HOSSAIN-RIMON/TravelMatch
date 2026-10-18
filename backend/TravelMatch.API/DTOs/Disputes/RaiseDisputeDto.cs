using TravelMatch.API.Models;

namespace TravelMatch.API.DTOs.Disputes;

public class RaiseDisputeDto
{
    public DisputeRelatedTripType TripType { get; set; }
    public int TripId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
