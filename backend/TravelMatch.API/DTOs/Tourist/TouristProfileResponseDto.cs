namespace TravelMatch.API.DTOs.Tourist;

public class TouristProfileResponseDto
{
    public int UserId { get; set; }
    public int ProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Preferences { get; set; } = string.Empty;
}
