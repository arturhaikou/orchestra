using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Data access abstraction for <see cref="AgentSkill"/> join-table operations.
/// </summary>
public interface IAgentSkillDataAccess
{
    /// <summary>
    /// Retrieves all <see cref="Skill"/> entities assigned to the specified agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of skills assigned to the agent.</returns>
    Task<List<Skill>> GetSkillsByAgentIdAsync(Guid agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a set of skills to an agent. Ignores assignments that already exist (upsert behaviour).
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="skillIds">Skill IDs to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AssignSkillsAsync(Guid agentId, IReadOnlyList<Guid> skillIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all skill assignments for the specified agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAllSkillsAsync(Guid agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all skill assignments for an agent with the provided set.
    /// A no-op when <paramref name="skillIds"/> is empty (removes all existing assignments).
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="skillIds">The new complete set of skill IDs to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReplaceSkillsAsync(Guid agentId, IReadOnlyList<Guid> skillIds, CancellationToken cancellationToken = default);
}
