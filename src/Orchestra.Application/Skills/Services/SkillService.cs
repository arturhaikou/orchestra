using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Skills.DTOs;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.Skills.Services;

/// <summary>
/// Application service for creating, reading, updating, and deleting workspace skills.
/// </summary>
public class SkillService : ISkillService
{
    private readonly ISkillDataAccess _skillDataAccess;
    private readonly IWorkspaceAuthorizationService _authorizationService;

    public SkillService(ISkillDataAccess skillDataAccess, IWorkspaceAuthorizationService authorizationService)
    {
        _skillDataAccess = skillDataAccess ?? throw new ArgumentNullException(nameof(skillDataAccess));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    /// <inheritdoc />
    public async Task<List<SkillDto>> GetSkillsAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var skills = await _skillDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        return skills.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<SkillDto?> GetSkillByIdAsync(
        Guid userId,
        Guid workspaceId,
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var skill = await _skillDataAccess.GetByIdAsync(skillId, cancellationToken);
        if (skill is null || skill.WorkspaceId != workspaceId)
            return null;

        return MapToDto(skill);
    }

    /// <inheritdoc />
    public async Task<SkillDto> CreateSkillAsync(
        Guid userId,
        CreateSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, cancellationToken);

        var skill = Skill.Create(request.WorkspaceId, request.Name, request.Description, request.Instructions);
        await _skillDataAccess.AddAsync(skill, cancellationToken);

        return MapToDto(skill);
    }

    /// <inheritdoc />
    public async Task<SkillDto?> UpdateSkillAsync(
        Guid userId,
        Guid workspaceId,
        Guid skillId,
        UpdateSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var skill = await _skillDataAccess.GetByIdAsync(skillId, cancellationToken);
        if (skill is null || skill.WorkspaceId != workspaceId)
            return null;

        skill.Update(request.Name, request.Description, request.Instructions);
        await _skillDataAccess.UpdateAsync(skill, cancellationToken);

        return MapToDto(skill);
    }

    /// <inheritdoc />
    public async Task DeleteSkillAsync(
        Guid userId,
        Guid workspaceId,
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var exists = await _skillDataAccess.ExistsInWorkspaceAsync(skillId, workspaceId, cancellationToken);
        if (!exists)
            return;

        await _skillDataAccess.DeleteAsync(skillId, cancellationToken);
    }

    private static SkillDto MapToDto(Skill skill) =>
        new(
            Id: skill.Id.ToString(),
            WorkspaceId: skill.WorkspaceId.ToString(),
            Name: skill.Name,
            Description: skill.Description,
            Instructions: skill.Instructions,
            CreatedAt: skill.CreatedAt,
            UpdatedAt: skill.UpdatedAt
        );
}
