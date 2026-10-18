using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.TripRequests;

/// <summary>
/// Tourist updates their own trip request.
/// Only allowed while the request is still Pending.
/// All fields are optional — only provided fields are applied.
/// </summary>
public class UpdateTripRequestDto
{
    [MaxLength(200)]
    public string? Destination { get; set; }

    [MaxLength(100)]
    public string? TripType { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Range(1, 100)]
    public int? NumberOfTravelers { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? Budget { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? BudgetMin { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? BudgetMax { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? TravelPreferences { get; set; }
}
