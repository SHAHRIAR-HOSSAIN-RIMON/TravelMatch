namespace TravelMatch.API.DTOs.Admin;

public class AdminUserListResultDto
{
    public bool Success { get; set; }

    public AdminError Error { get; set; } = AdminError.None;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<AdminUserDto> Data { get; set; } = Array.Empty<AdminUserDto>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
