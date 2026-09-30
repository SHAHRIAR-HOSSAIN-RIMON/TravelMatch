using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Tourist;

public class UpdateTouristProfileDto
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Preferences { get; set; }
}
