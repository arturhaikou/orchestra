using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Application.Workspaces.Services;

/// <summary>
/// Service for validating workspace membership and authorization.
/// </summary>
public class WorkspaceAuthorizationService : IWorkspaceAuthorizationService
{
    private readonly IWorkspaceDataAccess _workspaceDataAccess;

    public WorkspaceAuthorizationService(IWorkspaceDataAccess workspaceDataAccess)
    {
        _workspaceDataAccess = workspaceDataAccess ?? throw new ArgumentNullException(nameof(workspaceDataAccess));
    }

    public async Task ValidateMembershipAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var isMember = await _workspaceDataAccess.IsMemberAsync(userId, workspaceId, cancellationToken);

        if (!isMember)
        {
            throw new WorkspaceAccessDeniedException(userId, workspaceId);
        }
    }

    public async Task EnsureUserIsMemberAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var isMember = await _workspaceDataAccess.IsMemberAsync(userId, workspaceId, cancellationToken);

        if (!isMember)
        {
            throw new UnauthorizedWorkspaceAccessException(userId, workspaceId);
        }
    }

    public async Task<bool> IsMemberAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _workspaceDataAccess.IsMemberAsync(userId, workspaceId, cancellationToken);
    }

    public async Task EnsureUserIsOwnerAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        // Stage 1: membership check — non-members receive UnauthorizedWorkspaceAccessException (→ 404).
        var isMember = await _workspaceDataAccess.IsMemberAsync(userId, workspaceId, cancellationToken);
        if (!isMember)
        {
            throw new UnauthorizedWorkspaceAccessException(userId, workspaceId);
        }

        // Stage 2: ownership check — members who are not the owner receive WorkspaceForbiddenException (→ 403).
        var workspace = await _workspaceDataAccess.GetByIdAsync(workspaceId, cancellationToken);
        if (workspace is null)
        {
            // Workspace disappeared between the membership and ownership checks (unlikely but safe).
            throw new UnauthorizedWorkspaceAccessException(userId, workspaceId);
        }

        if (workspace.OwnerId != userId)
        {
            throw new WorkspaceForbiddenException(userId, workspaceId);
        }
    }
}