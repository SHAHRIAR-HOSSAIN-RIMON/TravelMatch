namespace TravelMatch.API.DTOs.Admin;

public class AdminUserStatusResultDto
{
    public bool Success { get; set; }

    public AdminError Error { get; set; } = AdminError.None;

    public string Message { get; set; } = string.Empty;

    public AdminUserDto? Data { get; set; }
}
