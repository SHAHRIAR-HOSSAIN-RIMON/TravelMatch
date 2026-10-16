namespace TravelMatch.API.DTOs.TripRequests;

public class TripRequestListResultDto
{
    public bool Success { get; set; }

    public TripRequestError Error { get; set; } = TripRequestError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<TripRequestResponseDto> Data { get; set; }
        = Array.Empty<TripRequestResponseDto>();

    public int TotalCount { get; set; }
}
