namespace TravelMatch.API.Models;

public enum TripRequestStatus
{
    Pending,
    ProposalsReceived,
    Matched,
    PaymentPending,
    Planned,
    InProgress,
    Completed,
    Cancelled
}