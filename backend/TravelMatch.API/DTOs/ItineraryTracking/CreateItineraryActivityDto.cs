using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.ItineraryTracking;

public class CreateItineraryActivityDto
{
    public int ItineraryDayId { get; set; }

    public int OrderIndex { get; set; } = -1;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
}