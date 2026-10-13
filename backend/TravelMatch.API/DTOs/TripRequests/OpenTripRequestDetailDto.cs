namespace TravelMatch.API.DTOs.TripRequests;

public class OpenTripRequestDetailDto
{
    public int Id { get; set; }

    public string Destination { get; set; } = string.Empty;

    public string TripType { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int NumberOfDays { get; set; }

    public int NumberOfTravelers { get; set; }

    public decimal Budget { get; set; }

    public decimal BudgetMin { get; set; }

    public decimal BudgetMax { get; set; }

    public string Description { get; set; } = string.Empty;

    public string TravelPreferences { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}