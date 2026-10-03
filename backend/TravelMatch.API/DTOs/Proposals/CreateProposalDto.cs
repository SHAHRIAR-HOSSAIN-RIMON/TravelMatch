using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Proposals;

public class CreateProposalDto
{
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? EstimatedExpenses { get; set; }

    [Required]
    [MaxLength(300)]
    public string Availability { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Inclusions { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Exclusions { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }
}