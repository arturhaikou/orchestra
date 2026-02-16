using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Data access contract for integration persistence operations.
/// </summary>
public interface IIntegrationDataAccess
{
    /// <summary>
    /// Retrieves all active integrations for a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active integrations ordered by Type and Name.</returns>
    Task<List<Integration>> GetByWorkspaceIdAsync(
        Guid workspaceId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific integration by ID if active.
    /// </summary>
    /// <param name="integrationId">The integration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The integration entity or null if not found.</returns>
    Task<Integration?> GetByIdAsync(
        Guid integrationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an integration with the specified name exists in the workspace.
    /// </summary>
    /// <param name="name">The integration name to check.</param>
    /// <param name="workspaceId">The workspace ID to scope the check.</param>
    /// <param name="excludeIntegrationId">Optional integration ID to exclude from check (for update scenarios).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a duplicate name exists, false otherwise.</returns>
    Task<bool> ExistsByNameInWorkspaceAsync(
        string name, 
        Guid workspaceId, 
        Guid? excludeIntegrationId = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new integration to the database.
    /// </summary>
    /// <param name="integration">The integration entity to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(Integration integration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing integration in the database.
    /// </summary>
    /// <param name="integration">The integration entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(Integration integration, CancellationToken cancellationToken = default);
}
