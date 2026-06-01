using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

public interface ISkillFolderDataAccess
{
    Task<SkillFolder?> GetByIdAsync(Guid skillFolderId, CancellationToken cancellationToken = default);

    Task<List<SkillFolder>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    Task AddAsync(SkillFolder skillFolder, CancellationToken cancellationToken = default);

    Task UpdateAsync(SkillFolder skillFolder, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid skillFolderId, CancellationToken cancellationToken = default);

    Task<bool> ExistsInWorkspaceAsync(Guid skillFolderId, Guid workspaceId, CancellationToken cancellationToken = default);
}
