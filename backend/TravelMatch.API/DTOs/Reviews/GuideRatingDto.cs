namespace TravelMatch.API.DTOs.Reviews;

public class GuideRatingDto
{
    public int GuideId { get; set; }
    public decimal? AverageRating { get; set; }
    public int ReviewCount { get; set; }
}