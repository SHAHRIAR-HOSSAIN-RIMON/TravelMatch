namespace TravelMatch.API.DTOs.Disputes;

public class DisputeResultDto
{
    public bool Success { get; set; }
    public DisputeError Error { get; set; } = DisputeError.None;
    public string Message { get; set; } = string.Empty;
    public DisputeResponseDto? Data { get; set; }
}
