namespace Orchestra.Application.Common.Interfaces;

public interface IAgentToolActionDataAccess
{
    /// <summary>
    /// Gets all tool action IDs assigned to a specific agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tool action IDs assigned to the agent.</returns>
    Task<List<Guid>> GetToolActionIdsByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unique tool category names for all tool actions assigned to a specific agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of distinct tool category names.</returns>
    Task<List<string>> GetUniqueCategoryNamesByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns multiple tool actions to an agent. Ignores duplicates (upsert behavior).
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="toolActionIds">List of tool action IDs to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AssignToolActionsAsync(
        Guid agentId,
        List<Guid> toolActionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes specific tool action assignments from an agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="toolActionIds">List of tool action IDs to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveToolActionsAsync(
        Guid agentId,
        List<Guid> toolActionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all tool action assignments for a specific agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAllToolActionsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);
}