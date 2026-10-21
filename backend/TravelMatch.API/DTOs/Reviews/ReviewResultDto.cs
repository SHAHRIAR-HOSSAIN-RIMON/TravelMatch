namespace TravelMatch.API.DTOs.Reviews;

public class ReviewResultDto
{
    public bool Success { get; set; }

    public ReviewError Error { get; set; } = ReviewError.None;

    public string Message { get; set; } = string.Empty;

    public ReviewResponseDto? Data { get; set; }
}