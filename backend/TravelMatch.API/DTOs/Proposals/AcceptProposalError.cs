namespace TravelMatch.API.DTOs.Proposals;

public class AcceptProposalError
{
    public const int None = 0;
    public const int TripRequestNotFound = 1;
    public const int ProposalNotFound = 2;
    public const int Unauthorized = 3;
    public const int AlreadyAccepted = 4;
    public const int ProposalNotAvailable = 5;
    public const int TripRequestAlreadyMatched = 6;
    public const int ServerError = 7;
    public const int TripRequestNotAvailable = 8;
}
