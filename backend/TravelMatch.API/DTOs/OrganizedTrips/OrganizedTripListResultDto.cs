namespace TravelMatch.API.DTOs.OrganizedTrips;

public class OrganizedTripListResultDto
{
    public bool Success { get; set; }

    public OrganizedTripError Error { get; set; } = OrganizedTripError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<OrganizedTripResponseDto>? Data { get; set; }

    public int TotalCount { get; set; }
}
