using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

public interface IAgentSkillFolderDataAccess
{
    Task<List<SkillFolder>> GetFoldersByAgentIdAsync(Guid agentId, CancellationToken cancellationToken = default);

    Task AssignFoldersAsync(Guid agentId, IReadOnlyList<Guid> skillFolderIds, CancellationToken cancellationToken = default);

    Task RemoveAllFoldersAsync(Guid agentId, CancellationToken cancellationToken = default);

    Task ReplaceFoldersAsync(Guid agentId, IReadOnlyList<Guid> skillFolderIds, CancellationToken cancellationToken = default);
}
