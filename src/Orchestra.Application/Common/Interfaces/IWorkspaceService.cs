using Orchestra.Application.Workspaces.DTOs;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Interface for workspace services.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Creates a new workspace asynchronously with the user as owner.
    /// </summary>
    /// <param name="userId">The ID of the user creating the workspace.</param>
    /// <param name="request">The create workspace request containing workspace details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the created workspace DTO.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    Task<WorkspaceDto> CreateWorkspaceAsync(Guid userId, CreateWorkspaceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all workspaces where the specified user is a member.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of workspace DTOs containing id and name.</returns>
    Task<WorkspaceDto[]> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a workspace name. Only the workspace owner can perform this operation.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="workspaceId">The workspace ID to update.</param>
    /// <param name="request">The update request containing the new workspace name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated workspace DTO.</returns>
    /// <exception cref="WorkspaceNotFoundException">Thrown when workspace is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user is not the owner.</exception>
    Task<WorkspaceDto> UpdateWorkspaceAsync(
        Guid userId, 
        Guid workspaceId, 
        UpdateWorkspaceRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace asynchronously. Only the workspace owner can delete a workspace.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user requesting deletion.</param>
    /// <param name="workspaceId">The ID of the workspace to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="WorkspaceNotFoundException">Thrown when workspace is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user is not the workspace owner.</exception>
    Task DeleteWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);
}