using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Proposals;

/// <summary>
/// Guide edits a proposal that is still in Draft status.
/// All fields are optional — only provided fields are updated.
/// </summary>
public class UpdateProposalDto
{
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? ProposedPrice { get; set; }

    [MaxLength(2000)]
    public string? Message { get; set; }

    [MaxLength(500)]
    public string? Availability { get; set; }

    [MaxLength(2000)]
    public string? Inclusions { get; set; }

    [MaxLength(2000)]
    public string? Exclusions { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
