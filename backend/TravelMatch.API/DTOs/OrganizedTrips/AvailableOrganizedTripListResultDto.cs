namespace TravelMatch.API.DTOs.OrganizedTrips;

public class AvailableOrganizedTripListResultDto
{
    public bool Success { get; set; }

    public OrganizedTripError Error { get; set; } = OrganizedTripError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<AvailableOrganizedTripDto> Data { get; set; } = Array.Empty<AvailableOrganizedTripDto>();

    public int TotalCount { get; set; }
}
