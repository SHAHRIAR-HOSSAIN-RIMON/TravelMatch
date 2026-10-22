namespace TravelMatch.API.DTOs.Admin;

/// <summary>Admin-facing user projection; deliberately excludes authentication fields.</summary>
public class AdminUserDto
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
