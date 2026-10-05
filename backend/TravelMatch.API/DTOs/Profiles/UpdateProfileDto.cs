using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Profiles;

public class UpdateProfileDto
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public GuideProfileUpdateDto? Guide { get; set; }

    public TouristProfileUpdateDto? Tourist { get; set; }

    public OrganizerProfileUpdateDto? Organizer { get; set; }
}
