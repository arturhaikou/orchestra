using Orchestra.Application.Skills.DTOs;

namespace Orchestra.Application.Skills.Services;

public interface ISkillFolderService
{
    Task<List<SkillFolderDto>> GetSkillFoldersAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    Task<SkillFolderDto?> GetSkillFolderByIdAsync(Guid userId, Guid workspaceId, Guid skillFolderId, CancellationToken cancellationToken = default);

    Task<SkillFolderDto> CreateSkillFolderAsync(Guid userId, CreateSkillFolderRequest request, CancellationToken cancellationToken = default);

    Task<SkillFolderDto?> UpdateSkillFolderAsync(Guid userId, Guid workspaceId, Guid skillFolderId, UpdateSkillFolderRequest request, CancellationToken cancellationToken = default);

    Task DeleteSkillFolderAsync(Guid userId, Guid workspaceId, Guid skillFolderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscoveredSkillDto>> GetAvailableSkillsAsync(Guid userId, Guid workspaceId, Guid skillFolderId, CancellationToken cancellationToken = default);
}
