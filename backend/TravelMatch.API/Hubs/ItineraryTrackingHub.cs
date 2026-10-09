using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Hubs;

[Authorize]
public class ItineraryTrackingHub : Hub
{
    private readonly IItineraryTrackingService _trackingService;
    private readonly ILogger<ItineraryTrackingHub> _logger;

    public ItineraryTrackingHub(
        IItineraryTrackingService trackingService,
        ILogger<ItineraryTrackingHub> logger)
    {
        _trackingService = trackingService;
        _logger = logger;
    }

    public async Task JoinProposalGroup(int proposalId)
    {
        var userId = GetUserId();
        if (userId == 0)
        {
            _logger.LogWarning("Unauthenticated user attempted to join proposal group {ProposalId}", proposalId);
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var isAuthorized = await _trackingService.IsProposalParticipantAsync(userId, proposalId);
        if (!isAuthorized)
        {
            _logger.LogWarning("User {UserId} not authorized for proposal {ProposalId}", userId, proposalId);
            await Clients.Caller.SendAsync("Error", "Not authorized to access this itinerary");
            return;
        }

        var groupName = $"proposal-{proposalId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} joined group {GroupName}", userId, groupName);
        
        await Clients.Caller.SendAsync("JoinedGroup", groupName);
    }

    public async Task LeaveProposalGroup(int proposalId)
    {
        var groupName = $"proposal-{proposalId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} left group {GroupName}", GetUserId(), groupName);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} disconnected", userId);
        await base.OnDisconnectedAsync(exception);
    }

    private int GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

}