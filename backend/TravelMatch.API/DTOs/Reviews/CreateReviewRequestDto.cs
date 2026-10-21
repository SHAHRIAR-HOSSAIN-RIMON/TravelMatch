using System.ComponentModel.DataAnnotations;

namespace TravelMatch.API.DTOs.Reviews;

public class CreateReviewRequestDto
{
    /// <summary>
    /// The ID of the guide being reviewed.
    /// </summary>
    [Required]
    public int GuideId { get; set; }

    /// <summary>
    /// The ID of the completed trip being reviewed.
    /// </summary>
    [Required]
    public int TripRequestId { get; set; }

    /// <summary>
    /// Rating from 1 to 5.
    /// </summary>
    [Required]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }

    /// <summary>
    /// Optional comment for the review.
    /// </summary>
    [MaxLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters.")]
    public string? Comment { get; set; }
}