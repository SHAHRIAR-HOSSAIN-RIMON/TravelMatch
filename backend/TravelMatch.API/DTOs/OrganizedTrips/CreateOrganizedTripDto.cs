using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.OrganizedTrips;

public class CreateOrganizedTripDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Destination { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxParticipants { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal PricePerPerson { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Inclusions { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Exclusions { get; set; }

    [Required]
    public DateOnly RegistrationDeadline { get; set; }
}
