namespace TravelMatch.API.DTOs.Admin;

public enum AdminError
{
    None,
    AdminOnly,
    UserNotFound,
    InvalidInput,
    CannotDeactivateSelf
}
