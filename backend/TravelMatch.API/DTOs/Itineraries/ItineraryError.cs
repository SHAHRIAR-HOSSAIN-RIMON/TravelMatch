namespace TravelMatch.API.DTOs.Itineraries;

public enum ItineraryError
{
    None,
    ProposalNotFound,
    Unauthorized,
    CannotEditRejectedProposal,
    DuplicateDayNumber,
    InvalidDayNumber,
    TitleRequired,
    ActivitiesRequired,
    ServerError
}
