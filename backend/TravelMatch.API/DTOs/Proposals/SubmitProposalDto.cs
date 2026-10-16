using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Proposals;

public class SubmitProposalDto
{
    [Required]
    [MaxLength(500)]
    public string Availability { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Inclusions { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Exclusions { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
