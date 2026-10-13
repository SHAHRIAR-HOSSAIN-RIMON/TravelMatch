
using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Tourist;

public class UpdateTouristProfileRequestDto
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    [RegularExpression(
        @"^$|^[+]?[0-9\s\-()]{6,20}$",
        ErrorMessage = "PhoneNumber must be a valid phone number.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(1000)]
    public string? Preferences { get; set; }
}
