using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.TripRequests;

public class UpdateTripRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Destination { get; set; } = string.Empty;

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
}
