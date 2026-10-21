namespace TravelMatch.API.DTOs.Reviews;

public class ReviewResponseDto
{
    public int Id { get; set; }
    public int TouristId { get; set; }
    public string TouristName { get; set; } = string.Empty;
    public int GuideId { get; set; }
    public string GuideName { get; set; } = string.Empty;
    public int TripRequestId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}