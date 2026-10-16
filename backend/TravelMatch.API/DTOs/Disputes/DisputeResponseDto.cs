namespace TravelMatch.API.DTOs.Disputes;

public class DisputeResponseDto
{
    public int Id { get; set; }
    public int ReportedByUserId { get; set; }
    public string TripType { get; set; } = string.Empty;
    public int TripId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
