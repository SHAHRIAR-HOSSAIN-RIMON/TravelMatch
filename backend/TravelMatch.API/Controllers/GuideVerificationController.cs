using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.GuideVerification;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Models;

namespace TravelMatch.API.Controllers;

[ApiController]
[Authorize]
[Route("api/guide-verifications")]
public sealed class GuideVerificationController : ControllerBase
{
    private readonly IGuideVerificationService _guideVerificationService;

    public GuideVerificationController(
        IGuideVerificationService guideVerificationService)
    {
        _guideVerificationService = guideVerificationService;
    }

    /// <summary>Submit profile information for Guide verification review.</summary>
    [HttpPost]
    [Authorize(Roles = "Guide")]
    [ProducesResponseType<GuideVerificationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<GuideVerificationResultDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitGuideVerificationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _guideVerificationService.SubmitAsync(
            userId,
            request,
            cancellationToken);

        return result.Success ? Ok(result) : ToStatusCode(result);
    }

    /// <summary>List all Guide verification submissions awaiting review.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<GuideVerificationSubmissionResponseDto>>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GuideVerificationSubmissionResponseDto>>>
        GetPending(CancellationToken cancellationToken)
    {
        var submissions = await _guideVerificationService.GetPendingAsync(
            cancellationToken);
        return Ok(submissions);
    }

    /// <summary>Retrieve a submitted Guide verification profile.</summary>
    [HttpGet("{guideProfileId:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<GuideVerificationSubmissionResponseDto>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuideVerificationSubmissionResponseDto>>
        GetSubmission(int guideProfileId, CancellationToken cancellationToken)
    {
        var submission = await _guideVerificationService.GetSubmissionAsync(
            guideProfileId,
            cancellationToken);
        return submission is null ? NotFound() : Ok(submission);
    }

    /// <summary>Approve a pending Guide verification submission.</summary>
    [HttpPost("{guideProfileId:int}/approve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<GuideVerificationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<GuideVerificationResultDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<GuideVerificationResultDto>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Approve(
        int guideProfileId,
        CancellationToken cancellationToken)
    {
        return Review(
            guideProfileId,
            VerificationStatus.Verified,
            cancellationToken);
    }

    /// <summary>Reje  pending Guide verification submission.</summary>
    [HttpPost("{guideProfileId:int}/reject")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<GuideVerificationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<GuideVerificationResultDto>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<GuideVerificationResultDto>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Reject(
        int guideProfileId,
        CancellationToken cancellationToken)
    {
        return Review(
            guideProfileId,
            VerificationStatus.Rejected,
            cancellationToken);
    }

    private async Task<IActionResult> Review(
        int guideProfileId,
        VerificationStatus decision,
        CancellationToken cancellationToken)
    {
        var result = await _guideVerificationService.ReviewAsync(
            guideProfileId,
            decision,
            cancellationToken);
        return result.Success ? Ok(result) : ToStatusCode(result);
    }

    private bool TryGetAuthenticatedUserId(out int userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out userId);
    }

    private IActionResult ToStatusCode(GuideVerificationResultDto result)
    {
        return result.Error switch
        {
            GuideVerificationError.GuideNotFound or
                GuideVerificationError.SubmissionNotFound =>
                    NotFound(result),
            GuideVerificationError.AlreadyPending or
                GuideVerificationError.AlreadyVerified or
                GuideVerificationError.NotPending =>
                    Conflict(result),
            _ => BadRequest(result)
        };
    }
}
