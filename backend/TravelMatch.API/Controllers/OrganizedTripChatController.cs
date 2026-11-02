using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TravelMatch.API.Hubs;
using TravelMatch.API.Services;

namespace TravelMatch.API.Controllers;

[ApiController]
[Authorize(Roles = "Organizer,Guide,Tourist")]
[Route("api/organized-trips/{tripId:int}/chat")]
public class OrganizedTripChatController(OrganizedTripChatService chat, IHubContext<OrganizedTripChatHub> hub,
    ILogger<OrganizedTripChatController> logger) : ControllerBase
{
    public class SendRequest
    {
        [Required, MaxLength(2000)] public string Content { get; set; } = string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> History(int tripId, [FromQuery] int? beforeId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        try { return Ok(await chat.HistoryAsync(userId, tripId, beforeId)); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { message = e.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Send(int tripId, SendRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        try
        {
            var message = await chat.SendAsync(userId, tripId, request.Content);
            // Re-read recipients after persistence. No message bodies go through SignalR.
            // A missed signal is recovered by history refresh/reconnect.
            try
            {
                var recipients = (await chat.MembersAsync(tripId)).Select(m => m.Id.ToString()).ToList();
                await hub.Clients.Users(recipients).SendAsync("ChatUpdated", tripId);
            }
            catch (Exception e) { logger.LogWarning(e, "Chat notification failed for trip {TripId}", tripId); }
            return Ok(message);
        }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
    }
}
