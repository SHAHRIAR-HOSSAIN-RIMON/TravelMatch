using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Profiles;

public class TouristProfileUpdateDto
{
    [MaxLength(1000)]
    public string? Preferences { get; set; }
}
