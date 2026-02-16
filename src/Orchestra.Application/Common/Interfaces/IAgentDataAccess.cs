using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Defines the contract for agent data access operations.
/// This interface abstracts the data access layer, allowing for decoupling
/// from specific persistence implementations like Entity Framework Core.
/// </summary>
public interface IAgentDataAccess
{
    /// <summary>
    /// Finds an agent by their unique identifier.
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The agent if found; otherwise, null.</returns>
    Task<Agent?> GetByIdAsync(Guid agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all agents for a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The unique identifier of the workspace.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of agents in the workspace.</returns>
    Task<List<Agent>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an agent exists by ID without loading the full entity (performance optimized).
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the agent exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(Guid agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new agent to the data store.
    /// </summary>
    /// <param name="agent">The agent to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task AddAsync(Agent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agent in the data store.
    /// </summary>
    /// <param name="agent">The agent to update.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateAsync(Agent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an agent from the data store by their unique identifier.
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteAsync(Guid agentId, CancellationToken cancellationToken = default);
}