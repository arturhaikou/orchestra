using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Ticket-specific authorization service implementation.
/// Wraps IWorkspaceAuthorizationService to provide cleaner APIs for ticket operations.
/// </summary>
public class TicketAuthorizationService : ITicketAuthorizationService
{
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;

    public TicketAuthorizationService(IWorkspaceAuthorizationService workspaceAuthorizationService)
    {
        _workspaceAuthorizationService = workspaceAuthorizationService;
    }

    /// <summary>
    /// Ensures user has access to a ticket's workspace.
    /// </summary>
    public async Task EnsureTicketAccessAsync(Guid userId, Ticket ticket, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(userId, ticket.WorkspaceId, cancellationToken);

        if (!hasAccess)
        {
            throw new UnauthorizedTicketAccessException(userId, ticket.Id.ToString());
        }
    }

    /// <summary>
    /// Ensures user has access to an integration's workspace (for external tickets).
    /// </summary>
    public async Task EnsureExternalTicketAccessAsync(Guid userId, Integration integration, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(userId, integration.WorkspaceId, cancellationToken);

        if (!hasAccess)
        {
            throw new UnauthorizedTicketAccessException(userId, $"integration:{integration.Id}");
        }
    }

    /// <summary>
    /// Ensures user can perform workspace ticket operations.
    /// </summary>
    public async Task EnsureWorkspaceAccessAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);
    }
}
