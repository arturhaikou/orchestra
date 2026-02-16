namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for validating workspace membership and authorization.
/// </summary>
public interface IWorkspaceAuthorizationService
{
    /// <summary>
    /// Validates that a user is a member of the specified workspace.
    /// </summary>
    /// <param name="userId">The user ID to validate.</param>
    /// <param name="workspaceId">The workspace ID to validate against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes successfully if the user is a member.</returns>
    /// <exception cref="WorkspaceAccessDeniedException">Thrown when the user is not a member of the workspace.</exception>
    Task ValidateMembershipAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that a user is a member of the specified workspace, throwing an exception if not.
    /// </summary>
    /// <param name="userId">The user ID to validate.</param>
    /// <param name="workspaceId">The workspace ID to validate against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes successfully if the user is a member.</returns>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when the user is not a member of the workspace.</exception>
    Task EnsureUserIsMemberAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user is a member of the specified workspace without throwing an exception.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="workspaceId">The workspace ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user is a member, false otherwise.</returns>
    Task<bool> IsMemberAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);
}