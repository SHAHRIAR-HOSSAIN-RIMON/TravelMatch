using Microsoft.AspNetCore.SignalR;
using TravelMatch.API.DTOs.Notifications;
using TravelMatch.API.Hubs;
using TravelMatch.API.Interfaces;
using TravelMatch.API.Services;

namespace TravelMatch.API.Tests;

/// <summary>
/// Test doubles for real-time notification delivery.
/// </summary>
public class NoopNotificationSender : INotificationRealtimeSender
{
    public Task SendToUserAsync(NotificationDto notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public class RecordingNotificationSender : INotificationRealtimeSender
{
    public List<NotificationDto> Sent { get; } = new();

    public Task SendToUserAsync(NotificationDto notification, CancellationToken cancellationToken = default)
    {
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

public class ThrowingNotificationSender : INotificationRealtimeSender
{
    public Task SendToUserAsync(NotificationDto notification, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("hub unavailable");
}

public class TestClientProxyGroup : IClientProxy
{
    public List<(string method, object?[] args)> Invocations { get; } = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Invocations.Add((method, args));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Minimal IHubContext fake: Clients.Group(name) returns a recordable proxy per group name.
/// </summary>
public class RecordingNotificationHubContext : IHubContext<NotificationHub>
{
    public Dictionary<string, TestClientProxyGroup> GroupProxies { get; } = new();

    public IHubClients Clients => new RecordingHubClients(GroupProxies);

    public IGroupManager Groups => throw new NotSupportedException();

    public HubConnectionContext Connection => throw new NotSupportedException();
}

public class RecordingHubClients : IHubClients
{
    private readonly Dictionary<string, TestClientProxyGroup> _groups;

    public RecordingHubClients(Dictionary<string, TestClientProxyGroup> groups)
    {
        _groups = groups;
    }

    public IClientProxy All => new TestClientProxyGroup();
    public IClientProxy Caller => new TestClientProxyGroup();

    public IClientProxy Client(string connectionId) => new TestClientProxyGroup();

    public IClientProxy Group(string groupName)
    {
        if (!_groups.TryGetValue(groupName, out var proxy))
        {
            proxy = new TestClientProxyGroup();
            _groups[groupName] = proxy;
        }
        return proxy;
    }

    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new TestClientProxyGroup();

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new TestClientProxyGroup();

    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new TestClientProxyGroup();

    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
        => Group(groupName);

    public IClientProxy User(string userId) => new TestClientProxyGroup();

    public IClientProxy Users(IReadOnlyList<string> userIds) => new TestClientProxyGroup();

    public IClientProxy Others => new TestClientProxyGroup();

    public IClientProxy OthersInGroup(string groupName) => new TestClientProxyGroup();
}
