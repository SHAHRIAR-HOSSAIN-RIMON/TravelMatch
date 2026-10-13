namespace TravelMatch.API.DTOs.TripRequests;

public class OpenTripRequestListResultDto
{
    public bool Success { get; set; }

    public OpenTripRequestError Error { get; set; } = OpenTripRequestError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<OpenTripRequestDto>? Data { get; set; }

    public int TotalCount { get; set; }

    public int TotalOpenCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages { get; set; }

    public bool HasFiltersApplied { get; set; }

    public string GuideVerificationStatus { get; set; } = string.Empty;

    public bool CanSubmitProposal { get; set; }
}