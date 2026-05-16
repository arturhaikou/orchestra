using Orchestra.Application.Skills.DTOs;

namespace Orchestra.Application.Skills.Services;

/// <summary>
/// Application service for managing workspace-scoped <see cref="Orchestra.Domain.Entities.Skill"/> entities.
/// </summary>
public interface ISkillService
{
    /// <summary>
    /// Returns all skills in the specified workspace.
    /// </summary>
    /// <param name="userId">The authenticated user performing the action.</param>
    /// <param name="workspaceId">The workspace to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<SkillDto>> GetSkillsAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single skill by ID, scoped to the workspace.
    /// </summary>
    /// <param name="userId">The authenticated user performing the action.</param>
    /// <param name="workspaceId">The workspace that owns the skill.</param>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The skill DTO, or <c>null</c> if not found in the workspace.</returns>
    Task<SkillDto?> GetSkillByIdAsync(Guid userId, Guid workspaceId, Guid skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new skill in the workspace.
    /// </summary>
    /// <param name="userId">The authenticated user performing the action.</param>
    /// <param name="request">The creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SkillDto> CreateSkillAsync(Guid userId, CreateSkillRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing skill's name, description, and instructions.
    /// </summary>
    /// <param name="userId">The authenticated user performing the action.</param>
    /// <param name="workspaceId">The workspace that owns the skill.</param>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated skill DTO, or <c>null</c> if not found in the workspace.</returns>
    Task<SkillDto?> UpdateSkillAsync(Guid userId, Guid workspaceId, Guid skillId, UpdateSkillRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a skill from the workspace. No-op if the skill does not exist.
    /// </summary>
    /// <param name="userId">The authenticated user performing the action.</param>
    /// <param name="workspaceId">The workspace that owns the skill.</param>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSkillAsync(Guid userId, Guid workspaceId, Guid skillId, CancellationToken cancellationToken = default);
}
