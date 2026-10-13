using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.GuideApplications;

public class CreateGuideApplicationDto
{
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal ProposedPrice { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;
}
