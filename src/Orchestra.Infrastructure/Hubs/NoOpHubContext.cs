using Microsoft.AspNetCore.SignalR;

namespace Orchestra.Infrastructure.Hubs;

/// <summary>
/// No-op implementation of <see cref="IHubContext{THub}"/> for non-web environments (e.g., Worker processes).
/// Discards all messages sent to clients. Used to allow services that require hub context
/// to run in background worker processes without SignalR infrastructure.
/// </summary>
public sealed class NoOpHubContext<THub> : IHubContext<THub> where THub : Hub
{
    /// <summary>
    /// No-op clients group that discards all messages.
    /// </summary>
    public IHubClients Clients => new NoOpHubClients();

    /// <summary>
    /// No-op groups manager that discards all operations.
    /// </summary>
    public IGroupManager Groups => new NoOpGroupManager();
}

/// <summary>
/// No-op implementation of <see cref="IHubClients"/> that discards all messages.
/// </summary>
internal sealed class NoOpHubClients : IHubClients
{
    public IClientProxy All => new NoOpClientProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new NoOpClientProxy();
    public IClientProxy Client(string connectionId) => new NoOpClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new NoOpClientProxy();
    public IClientProxy Group(string groupName) => new NoOpClientProxy();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new NoOpClientProxy();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new NoOpClientProxy();
    public IClientProxy Other(string connectionId) => new NoOpClientProxy();
    public IClientProxy OthersInGroup(string groupName) => new NoOpClientProxy();
    public IClientProxy User(string userId) => new NoOpClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new NoOpClientProxy();
}

/// <summary>
/// No-op implementation of <see cref="IClientProxy"/> that discards all messages.
/// </summary>
internal sealed class NoOpClientProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// No-op implementation of <see cref="IGroupManager"/> that discards all operations.
/// </summary>
internal sealed class NoOpGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveFromAllGroupsAsync(string connectionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
