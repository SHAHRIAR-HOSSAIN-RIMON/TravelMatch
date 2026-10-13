using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TravelMatch.API.DTOs.Messaging;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Hubs;

[Authorize]
public class TripChatHub : Hub
{
    private readonly ITripMessagingService _messagingService;
    private readonly ILogger<TripChatHub> _logger;

    public TripChatHub(
        ITripMessagingService messagingService,
        ILogger<TripChatHub> logger)
    {
        _messagingService = messagingService;
        _logger = logger;
    }

    public async Task JoinTripChat(int proposalId)
    {
        var userId = GetUserId();
        if (userId == 0)
        {
            _logger.LogWarning("Unauthenticated user attempted to join trip chat {ProposalId}", proposalId);
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var isAuthorized = await _messagingService.IsParticipantAsync(userId, proposalId);
        if (!isAuthorized)
        {
            _logger.LogWarning("User {UserId} not authorized for trip chat {ProposalId}", userId, proposalId);
            await Clients.Caller.SendAsync("Error", "Not authorized to access this trip conversation");
            return;
        }

        var groupName = GetGroupName(proposalId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} joined trip chat group {GroupName}", userId, groupName);

        await Clients.Caller.SendAsync("JoinedGroup", groupName);
    }

    public async Task JoinProposalGroup(int proposalId)
    {
        await JoinTripChat(proposalId);
    }

    public async Task LeaveTripChat(int proposalId)
    {
        var groupName = GetGroupName(proposalId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} left trip chat group {GroupName}", GetUserId(), groupName);
    }

    public async Task LeaveProposalGroup(int proposalId)
    {
        await LeaveTripChat(proposalId);
    }

    public async Task SendMessage(int proposalId, string content)
    {
        var userId = GetUserId();
        if (userId == 0)
        {
            _logger.LogWarning("Unauthenticated user attempted to send message to proposal {ProposalId}", proposalId);
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var request = new SendMessageDto
        {
            ProposalId = proposalId,
            Content = content
        };

        var result = await _messagingService.SendMessageAsync(userId, request);

        if (!result.Success || result.Data is null)
        {
            await Clients.Caller.SendAsync("Error", result.Message);
            return;
        }

        var groupName = GetGroupName(proposalId);

        // Broadcast to group only AFTER persistence succeeds
        await Clients.Group(groupName).SendAsync("ReceiveMessage", result.Data);
        await Clients.Caller.SendAsync("MessageSent", result.Data);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} disconnected from trip chat hub", userId);
        await base.OnDisconnectedAsync(exception);
    }

    private static string GetGroupName(int proposalId) => $"proposal-{proposalId}";

    private int GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }
}

