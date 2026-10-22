using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Admin;

/// <summary>Request body for PATCH /api/admin/users/{userId}/status.</summary>
public class UpdateUserStatusDto
{
    [Required]
    public bool? IsActive { get; set; }
}
