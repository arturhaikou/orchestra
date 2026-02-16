using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Defines the contract for workspace data access operations.
/// This interface abstracts the data access layer, allowing for decoupling
/// from specific persistence implementations like Entity Framework Core.
/// </summary>
public interface IWorkspaceDataAccess
{
    /// <summary>
    /// Creates a workspace and adds the owner to UserWorkspaces in a transaction.
    /// </summary>
    /// <param name="workspace">The workspace to create.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created workspace.</returns>
    Task<Workspace> CreateAsync(Workspace workspace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes made in the current context to the data store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active workspaces where the specified user is a member.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of workspaces ordered alphabetically by name.</returns>
    Task<List<Workspace>> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user is a member of the specified workspace.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="workspaceId">The workspace ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user is a member of the workspace, false otherwise.</returns>
    Task<bool> IsMemberAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user is a member of the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to check.</param>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user is a member of the workspace, false otherwise.</returns>
    Task<bool> IsUserMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a workspace by its unique identifier.
    /// </summary>
    /// <param name="id">The workspace ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The workspace if found; otherwise, null.</returns>
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing workspace in the database.
    /// </summary>
    /// <param name="workspace">The workspace entity with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace by its unique identifier.
    /// Cascade deletes all related UserWorkspaces due to foreign key configuration.
    /// </summary>
    /// <param name="id">The workspace ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="WorkspaceNotFoundException">Thrown when workspace is not found.</exception>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}