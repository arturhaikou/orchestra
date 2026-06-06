using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using System.Security.Claims;

namespace Orchestra.Infrastructure.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        ILogger<NotificationHub> logger)
    {
        _workspaceAuthorizationService = workspaceAuthorizationService
            ?? throw new ArgumentNullException(nameof(workspaceAuthorizationService));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task JoinWorkspaceGroup(Guid workspaceId)
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new HubException("Access denied.");
        }

        try
        {
            await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
                userId, workspaceId, Context.ConnectionAborted);
        }
        catch (UnauthorizedWorkspaceAccessException)
        {
            _logger.LogWarning(
                "SignalR access denied to workspace group. ConnectionId={ConnectionId} WorkspaceId={WorkspaceId}",
                Context.ConnectionId, workspaceId);
            throw new HubException("Access denied.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId, $"workspace-{workspaceId}", Context.ConnectionAborted);

        _logger.LogDebug(
            "SignalR client joined workspace group. ConnectionId={ConnectionId} WorkspaceId={WorkspaceId}",
            Context.ConnectionId, workspaceId);
    }

    public async Task LeaveWorkspaceGroup(Guid workspaceId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId, $"workspace-{workspaceId}", Context.ConnectionAborted);

        _logger.LogDebug(
            "SignalR client left workspace group. ConnectionId={ConnectionId} WorkspaceId={WorkspaceId}",
            Context.ConnectionId, workspaceId);
    }

    public async Task JoinUserGroup()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new HubException("Access denied.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId, $"user-{userIdClaim.ToLower()}", Context.ConnectionAborted);

        _logger.LogDebug(
            "SignalR client joined user group. ConnectionId={ConnectionId} UserId={UserId}",
            Context.ConnectionId, userIdClaim);
    }

    public async Task LeaveUserGroup()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return;

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId, $"user-{userIdClaim.ToLower()}", Context.ConnectionAborted);

        _logger.LogDebug(
            "SignalR client left user group. ConnectionId={ConnectionId} UserId={UserId}",
            Context.ConnectionId, userIdClaim);
    }
}
