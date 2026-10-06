using System.ComponentModel.DataAnnotations;
using TravelMatch.API.Models;

namespace TravelMatch.API.DTOs.GuideApplications;

public class OpenOrganizedTripDto
{
    public int Id { get; set; }

    public int OrganizerId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
