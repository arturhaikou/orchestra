using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Data access abstraction for <see cref="Skill"/> persistence operations.
/// </summary>
public interface ISkillDataAccess
{
    /// <summary>
    /// Retrieves a skill by its unique identifier.
    /// </summary>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The skill, or <c>null</c> if not found.</returns>
    Task<Skill?> GetByIdAsync(Guid skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all skills belonging to a workspace, ordered by creation date descending.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of skills in the workspace.</returns>
    Task<List<Skill>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new skill to the store.
    /// </summary>
    /// <param name="skill">The skill entity to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(Skill skill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing skill.
    /// </summary>
    /// <param name="skill">The updated skill entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Skill skill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a skill by its unique identifier.
    /// </summary>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when the skill exists and belongs to the specified workspace.
    /// </summary>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="workspaceId">The workspace to check ownership against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ExistsInWorkspaceAsync(Guid skillId, Guid workspaceId, CancellationToken cancellationToken = default);
}
