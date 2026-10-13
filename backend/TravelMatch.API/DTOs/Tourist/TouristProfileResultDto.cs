
namespace TravelMatch.API.DTOs.Tourist;

public class TouristProfileResultDto
{
    public bool Success { get; set; }

    public TouristProfileError Error { get; set; } = TouristProfileError.None;

    public string Message { get; set; } = string.Empty;

    public TouristProfileResponseDto? Data { get; set; }
}
