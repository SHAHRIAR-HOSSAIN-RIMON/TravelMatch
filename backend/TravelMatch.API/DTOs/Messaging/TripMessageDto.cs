namespace TravelMatch.API.DTOs.Messaging;

public class TripMessageDto
{
    public int Id { get; set; }

    public int ProposalId { get; set; }

    public int SenderId { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public string SenderRole { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }
}

