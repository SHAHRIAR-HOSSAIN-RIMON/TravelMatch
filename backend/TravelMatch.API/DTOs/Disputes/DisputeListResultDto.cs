namespace TravelMatch.API.DTOs.Disputes;

public class DisputeListResultDto
{
    public bool Success { get; set; }

    public DisputeError Error { get; set; } = DisputeError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<DisputeResponseDto> Data { get; set; } = Array.Empty<DisputeResponseDto>();

    public int TotalCount { get; set; }
}