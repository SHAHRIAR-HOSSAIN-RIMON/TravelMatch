namespace TravelMatch.API.DTOs.GuideVerification;

/// <summary>Paged result for the list-pending-guides endpoint.</summary>
public class GuideVerificationListResultDto
{
    public bool Success { get; set; }

    public GuideVerificationError Error { get; set; } = GuideVerificationError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<GuideVerificationResponseDto> Data { get; set; }
        = Array.Empty<GuideVerificationResponseDto>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
