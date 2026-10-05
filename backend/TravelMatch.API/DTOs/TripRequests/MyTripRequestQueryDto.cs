using TravelMatch.API.Models;

namespace TravelMatch.API.DTOs.TripRequests;

public class MyTripRequestQueryDto
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
    public const int MaxPage = 10_000;

    public TripRequestStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;
}
