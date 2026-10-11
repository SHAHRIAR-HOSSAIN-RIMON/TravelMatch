using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Messaging;

public class SendMessageDto
{
    [Required]
    public int ProposalId { get; set; }

    [Required]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message content must be between 1 and 2000 characters.")]
    public string Content { get; set; } = string.Empty;
}

