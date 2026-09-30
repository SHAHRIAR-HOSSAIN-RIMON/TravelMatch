using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.TripRequests;

public class CreateTripRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Destination { get; set; } = string.Empty;

    [MaxLength(80)]
    public string TripType { get; set; } = "Other";

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Range(1, 100)]
    public int NumberOfTravelers { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Budget { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? TravelPreferences { get; set; }
}
