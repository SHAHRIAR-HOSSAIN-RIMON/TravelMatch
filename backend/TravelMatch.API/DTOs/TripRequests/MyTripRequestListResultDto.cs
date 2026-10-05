namespace TravelMatch.API.DTOs.TripRequests;

public class MyTripRequestListResultDto
{
    public bool Success { get; set; }

    public TripRequestError Error { get; set; } = TripRequestError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<TripRequestResponseDto>? Data { get; set; }

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages { get; set; }

    public string? StatusFilter { get; set; }
}
