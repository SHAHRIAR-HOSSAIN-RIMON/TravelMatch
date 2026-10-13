namespace TravelMatch.API.DTOs.TripRequests;

public class OpenTripRequestQueryDto
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 10_000;

    public string? Destination { get; set; }

    public string? TripType { get; set; }

    public DateOnly? StartDateFrom { get; set; }

    public DateOnly? StartDateTo { get; set; }

    public decimal? MinBudget { get; set; }

    public decimal? MaxBudget { get; set; }

    public OpenTripRequestSort Sort { get; set; } = OpenTripRequestSort.Newest;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;
}