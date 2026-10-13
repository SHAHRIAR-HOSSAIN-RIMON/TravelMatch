
namespace TravelMatch.API.DTOs.Tourist;

public enum TouristProfileError
{
    None,
    ProfileNotFound,
    Unauthorized,
    InvalidFullName,
    InvalidPhoneNumber,
    PreferencesTooLong,
    AccountInactive
}
