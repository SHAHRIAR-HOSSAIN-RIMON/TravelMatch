namespace TravelMatch.API.DTOs.Profiles;

public class OrganizerProfileDto
{
    public int Id { get; set; }

    public string GroupOrganizationName { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;
}
