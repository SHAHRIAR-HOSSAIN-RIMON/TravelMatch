namespace TravelMatch.API.DTOs.OrganizedTrips;

public class AvailableOrganizedTripDetailResultDto
{
    public bool Success { get; set; }

    public OrganizedTripError Error { get; set; } = OrganizedTripError.None;

    public string Message { get; set; } = string.Empty;

    public AvailableOrganizedTripDto? Data { get; set; }
}
