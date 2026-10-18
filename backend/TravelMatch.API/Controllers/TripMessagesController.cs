using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TravelMatch.API.DTOs.Messaging;
using TravelMatch.API.Hubs;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

[ApiController]
[Route("api/trip-messages")]
[Authorize]
public class TripMessagesController : ControllerBase
{
    private readonly ITripMessagingService _messagingService;
    private readonly IHubContext<TripChatHub> _hubContext;
    private readonly ILogger<TripMessagesController> _logger;

    public TripMessagesController(
        ITripMessagingService messagingService,
        IHubContext<TripChatHub> hubContext,
        ILogger<TripMessagesController> logger)
    {
        _messagingService = messagingService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet("proposals/{proposalId:int}")]
    [HttpGet("{proposalId:int}")]
    public async Task<IActionResult> GetMessages(
        int proposalId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? beforeId = null)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _messagingService.GetMessagesAsync(userId, proposalId, page, pageSize, beforeId);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Error switch
        {
            TripMessagingError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripMessagingError.ProposalNotFound =>
                NotFound(result),

            TripMessagingError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost("proposals/{proposalId:int}")]
    [HttpPost("{proposalId:int}")]
    public async Task<IActionResult> SendMessage(
        int proposalId,
        [FromBody] SendMessageDto request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (request is null)
        {
            return BadRequest(new { message = "Request body cannot be null." });
        }

        request.ProposalId = proposalId;

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                message = "Validation failed.",
                errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => e.ErrorMessage)
                            .ToArray())
            });
        }

        var result = await _messagingService.SendMessageAsync(userId, request);

        if (result.Success && result.Data is not null)
        {
            await _hubContext.Clients.Group($"proposal-{proposalId}")
                .SendAsync("ReceiveMessage", result.Data);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            TripMessagingError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripMessagingError.ProposalNotFound =>
                NotFound(result),

            TripMessagingError.InvalidMessage =>
                BadRequest(result),

            TripMessagingError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost]
    public async Task<IActionResult> SendMessageDirect([FromBody] SendMessageDto request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (request is null)
        {
            return BadRequest(new { message = "Request body cannot be null." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                message = "Validation failed.",
                errors = ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => e.ErrorMessage)
                            .ToArray())
            });
        }

        var result = await _messagingService.SendMessageAsync(userId, request);

        if (result.Success && result.Data is not null)
        {
            await _hubContext.Clients.Group($"proposal-{request.ProposalId}")
                .SendAsync("ReceiveMessage", result.Data);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        return result.Error switch
        {
            TripMessagingError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripMessagingError.ProposalNotFound =>
                NotFound(result),

            TripMessagingError.InvalidMessage =>
                BadRequest(result),

            TripMessagingError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    [HttpPost("proposals/{proposalId:int}/messages/{messageId:int}/read")]
    [HttpPost("{proposalId:int}/messages/{messageId:int}/read")]
    public async Task<IActionResult> MarkMessageAsRead(int proposalId, int messageId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        var result = await _messagingService.MarkMessageAsReadAsync(userId, proposalId, messageId);

        if (result.Success)
        {
            // Real-time receipt only on the unread -> read transition.
            if (result.WasUnreadToRead && result.Data is not null)
            {
                await _hubContext.Clients.Group($"proposal-{proposalId}")
                    .SendAsync("MessageRead", result.Data);
            }

            return Ok(result);
        }

        return result.Error switch
        {
            TripMessagingError.Unauthorized =>
                StatusCode(StatusCodes.Status403Forbidden, result),

            TripMessagingError.MessageNotFound =>
                NotFound(result),

            TripMessagingError.ProposalNotFound =>
                NotFound(result),

            TripMessagingError.InvalidMessage =>
                BadRequest(result),

            TripMessagingError.ServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),

            _ =>
                BadRequest(result)
        };
    }

    private bool TryGetUserId(out int userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out userId);
    }
}

