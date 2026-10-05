using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Profiles;

public class GuideProfileUpdateDto
{
    [MaxLength(500)]
    public string? Bio { get; set; }

    [MaxLength(100)]
    public string? ServiceArea { get; set; }

    [Range(0, 80)]
    public int? ExperienceYears { get; set; }
}
