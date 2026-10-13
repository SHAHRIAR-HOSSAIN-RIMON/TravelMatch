using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Payments;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Tourist")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("proposal/{proposalId:int}")]
    public async Task<IActionResult> PayForProposal(
        [FromRoute] int proposalId,
        [FromBody] PayProposalRequestDto? request = null)
    {
        if (!TryGetTouristUserId(out var touristUserId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _paymentService.PayForProposalAsync(touristUserId, proposalId, request);

        if (result.Success)
        {
            return Ok(result);
        }

        return MapErrorToResponse(result);
    }

    [HttpPost("registration/{registrationId:int}")]
    public async Task<IActionResult> PayForRegistration(
        [FromRoute] int registrationId,
        [FromBody] PayRegistrationRequestDto? request = null)
    {
        if (!TryGetTouristUserId(out var touristUserId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var result = await _paymentService.PayForRegistrationAsync(touristUserId, registrationId, request);

        if (result.Success)
        {
            return Ok(result);
        }

        return MapErrorToResponse(result);
    }

    private IActionResult MapErrorToResponse(PaymentResultDto result)
    {
        return result.Error switch
        {
            PaymentError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            PaymentError.TouristProfileNotFound =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            PaymentError.ProposalNotFound =>
                NotFound(result),

            PaymentError.TripRequestNotFound =>
                NotFound(result),

            PaymentError.RegistrationNotFound =>
                NotFound(result),

            PaymentError.OrganizedTripNotFound =>
                NotFound(result),

            PaymentError.AlreadyPaid =>
                StatusCode(StatusCodes.Status409Conflict, result),

            PaymentError.InvalidProposalStatus =>
                StatusCode(StatusCodes.Status409Conflict, result),

            PaymentError.InvalidRegistrationStatus =>
                StatusCode(StatusCodes.Status409Conflict, result),

            PaymentError.TripCapacityExceeded =>
                StatusCode(StatusCodes.Status409Conflict, result),

            PaymentError.RegistrationDeadlinePassed =>
                StatusCode(StatusCodes.Status409Conflict, result),

            PaymentError.TripNotAvailableForRegistration =>
                StatusCode(StatusCodes.Status409Conflict, result),

            PaymentError.PaymentFailed =>
                StatusCode(StatusCodes.Status402PaymentRequired, result),

            PaymentError.InvalidRequest =>
                BadRequest(result),

            PaymentError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    private bool TryGetTouristUserId(out int touristUserId)
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out touristUserId);
    }
}
