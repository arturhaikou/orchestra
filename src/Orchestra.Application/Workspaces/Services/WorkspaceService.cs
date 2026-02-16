using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Application.Common.Exceptions;

namespace Orchestra.Application.Workspaces.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceDataAccess _workspaceDataAccess;

    public WorkspaceService(IWorkspaceDataAccess workspaceDataAccess)
    {
        _workspaceDataAccess = workspaceDataAccess;
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(Guid userId, CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = Workspace.Create(request.Name, userId);

        await _workspaceDataAccess.CreateAsync(workspace, cancellationToken);

        return new WorkspaceDto(workspace.Id.ToString(), workspace.Name);
    }

    public async Task<WorkspaceDto[]> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var workspaces = await _workspaceDataAccess.GetUserWorkspacesAsync(userId, cancellationToken);
        
        return workspaces
            .Select(w => new WorkspaceDto(w.Id.ToString(), w.Name))
            .ToArray();
    }

    public async Task<WorkspaceDto> UpdateWorkspaceAsync(
        Guid userId,
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Retrieve workspace
        var workspace = await _workspaceDataAccess.GetByIdAsync(
            workspaceId, 
            cancellationToken);
        
        if (workspace is null)
        {
            throw new WorkspaceNotFoundException(workspaceId);
        }
        
        // Check membership first (security: don't leak existence to non-members)
        var isMember = await _workspaceDataAccess.IsUserMemberAsync(workspaceId, userId, cancellationToken);
        if (!isMember)
        {
            // Return 404 to non-members (don't leak workspace existence)
            throw new WorkspaceNotFoundException(workspaceId);
        }
        
        // Verify ownership
        if (workspace.OwnerId != userId)
        {
            throw new UnauthorizedWorkspaceAccessException(
                "You do not have permission to update this workspace", 
                userId, 
                workspaceId);
        }
        
        // Update using domain method (validates and sets UpdatedAt)
        workspace.UpdateName(request.Name);
        
        // Persist changes
        await _workspaceDataAccess.UpdateAsync(workspace, cancellationToken);
        
        // Return DTO
        return new WorkspaceDto(workspace.Id.ToString(), workspace.Name);
    }

    public async Task DeleteWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceDataAccess.GetByIdAsync(workspaceId, cancellationToken);

        if (workspace == null)
        {
            throw new WorkspaceNotFoundException(workspaceId);
        }

        // Check membership first (security: don't leak existence to non-members)
        var isMember = await _workspaceDataAccess.IsUserMemberAsync(workspaceId, userId, cancellationToken);
        if (!isMember)
        {
            // Return 404 to non-members (don't leak workspace existence)
            throw new WorkspaceNotFoundException(workspaceId);
        }

        if (workspace.OwnerId != userId)
        {
            throw new UnauthorizedWorkspaceAccessException(
                "You do not have permission to delete this workspace", 
                userId, 
                workspaceId);
        }

        await _workspaceDataAccess.DeleteAsync(workspaceId, cancellationToken);
    }
}