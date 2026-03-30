using Orchestra.Domain.Enums;

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
    /// Returns the distinct external provider types required by an agent's assigned tool categories.
    /// <c>ProviderType.INTERNAL</c> is excluded because internal tools never use an integrationId.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of distinct <see cref="ProviderType"/> values (never includes <c>INTERNAL</c>).
    /// Returns an empty list when the agent has no external tool assignments.
    /// </returns>
    Task<List<ProviderType>> GetExternalProviderTypesByAgentIdAsync(
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

    /// <summary>
    /// Returns <c>true</c> if any of the provided tool action IDs correspond to a code review
    /// tool action — specifically those with the identifier <c>review_pull_request</c> (GitHub)
    /// or <c>review_merge_request</c> (GitLab).
    /// </summary>
    /// <param name="toolActionIds">The candidate tool action IDs to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> when at least one ID maps to a review tool action; otherwise <c>false</c>.
    /// Returns <c>false</c> for an empty collection.
    /// </returns>
    Task<bool> ContainsReviewToolActionAsync(
        IEnumerable<Guid> toolActionIds,
        CancellationToken cancellationToken = default);
}