namespace TravelMatch.API.Models;

public class TripMessage
{
    public int Id { get; set; }

    public int ProposalId { get; set; }

    public Proposal Proposal { get; set; } = null!;

    public int SenderId { get; set; }

    public User Sender { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
}

