namespace TravelMatch.API.DTOs.Profile;

public class ProfileResponseDto
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }

    public TouristProfileDto? TouristProfile { get; set; }

    public GuideProfileDto? GuideProfile { get; set; }

    public OrganizerProfileDto? OrganizerProfile { get; set; }
}
