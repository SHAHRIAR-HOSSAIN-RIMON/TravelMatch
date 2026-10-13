using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Itineraries;

public class CreateItineraryDayDto
{
    [Required]
    public int DayNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Activities { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Schedule { get; set; }
}
