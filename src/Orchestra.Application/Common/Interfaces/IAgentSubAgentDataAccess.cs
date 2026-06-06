namespace Orchestra.Application.Common.Interfaces;

public interface IAgentSubAgentDataAccess
{
    /// <summary>
    /// Gets all sub-agent IDs assigned to a specific parent agent.
    /// </summary>
    /// <param name="parentAgentId">The parent agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sub-agent IDs assigned to the parent agent.</returns>
    Task<List<Guid>> GetSubAgentIdsByParentAgentIdAsync(
        Guid parentAgentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all parent agent IDs that have the given agent assigned as a sub-agent.
    /// Used for circular reference detection.
    /// </summary>
    /// <param name="subAgentId">The sub-agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of parent agent IDs that reference the given sub-agent.</returns>
    Task<List<Guid>> GetParentAgentIdsBySubAgentIdAsync(
        Guid subAgentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns sub-agents to a parent agent. Skips already-existing assignments.
    /// </summary>
    /// <param name="parentAgentId">The parent agent identifier.</param>
    /// <param name="subAgentIds">The sub-agent IDs to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AssignSubAgentsAsync(
        Guid parentAgentId,
        List<Guid> subAgentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all sub-agent assignments for a parent agent.
    /// </summary>
    /// <param name="parentAgentId">The parent agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAllSubAgentsAsync(
        Guid parentAgentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all AgentSubAgents rows where the given agent is the sub-agent.
    /// Called before deleting an agent to satisfy the RESTRICT FK on SubAgentId.
    /// </summary>
    /// <param name="subAgentId">The agent being deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveBySubAgentIdAsync(
        Guid subAgentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a dictionary mapping each agent ID to its assigned sub-agent IDs
    /// for all agents in a workspace. Enables efficient batch loading without N+1 queries.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary keyed by parent agent ID with a list of sub-agent IDs as values.</returns>
    Task<Dictionary<Guid, List<Guid>>> GetSubAgentIdsByWorkspaceIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
