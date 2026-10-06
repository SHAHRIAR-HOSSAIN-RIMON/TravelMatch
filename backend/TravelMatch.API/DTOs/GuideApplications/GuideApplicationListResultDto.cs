namespace TravelMatch.API.DTOs.GuideApplications;

public class GuideApplicationListResultDto
{
    public bool Success { get; set; }

    public GuideApplicationError Error { get; set; } = GuideApplicationError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<GuideApplicationDto>? Data { get; set; }

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages { get; set; }
}
