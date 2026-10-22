namespace TravelMatch.API.DTOs.Admin;

public class AdminDashboardResultDto
{
    public bool Success { get; set; }

    public AdminError Error { get; set; } = AdminError.None;

    public string Message { get; set; } = string.Empty;

    public AdminDashboardSummaryDto? Data { get; set; }
}
