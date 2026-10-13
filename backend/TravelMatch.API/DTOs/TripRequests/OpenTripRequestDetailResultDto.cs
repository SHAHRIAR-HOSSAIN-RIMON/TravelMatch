namespace TravelMatch.API.DTOs.TripRequests;

public class OpenTripRequestDetailResultDto
{
    public bool Success { get; set; }

    public OpenTripRequestError Error { get; set; } = OpenTripRequestError.None;

    public string Message { get; set; } = string.Empty;

    public OpenTripRequestDetailDto? Data { get; set; }

    public string GuideVerificationStatus { get; set; } = string.Empty;

    public bool CanSubmitProposal { get; set; }
}