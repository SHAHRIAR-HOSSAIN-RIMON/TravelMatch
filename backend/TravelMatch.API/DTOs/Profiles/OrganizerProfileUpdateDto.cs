using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Profiles;

public class OrganizerProfileUpdateDto
{
    [MaxLength(253)]
    public string? GroupOrganizationName { get; set; }

    [MaxLength(2000)]
    public string? Bio { get; set; }
}
