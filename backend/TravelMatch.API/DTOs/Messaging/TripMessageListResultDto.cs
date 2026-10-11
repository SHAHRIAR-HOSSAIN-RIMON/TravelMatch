namespace TravelMatch.API.DTOs.Messaging;

public class TripMessageListResultDto
{
    public bool Success { get; set; }

    public TripMessagingError Error { get; set; } = TripMessagingError.None;

    public string Message { get; set; } = string.Empty;

    public List<TripMessageDto> Data { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public bool HasMore { get; set; }
}

