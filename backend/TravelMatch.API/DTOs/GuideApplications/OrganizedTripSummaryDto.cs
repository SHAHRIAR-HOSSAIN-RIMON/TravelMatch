namespace TravelMatch.API.DTOs.GuideApplications;

public class OrganizedTripSummaryDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Status { get; set; } = string.Empty;
}
