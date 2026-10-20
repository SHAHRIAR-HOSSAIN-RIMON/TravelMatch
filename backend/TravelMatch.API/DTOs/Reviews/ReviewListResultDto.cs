namespace TravelMatch.API.DTOs.Reviews;

public class ReviewListResultDto
{
    public bool Success { get; set; }

    public ReviewError Error { get; set; } = ReviewError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<ReviewResponseDto>? Data { get; set; }

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages { get; set; }
}
