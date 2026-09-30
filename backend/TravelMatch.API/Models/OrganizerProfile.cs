namespace TravelMatch.API.Models;

public class OrganizerProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string GroupOrganizationName { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;
}