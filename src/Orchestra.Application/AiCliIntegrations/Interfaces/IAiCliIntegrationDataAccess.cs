using Orchestra.Domain.Entities;

namespace Orchestra.Application.AiCliIntegrations.Interfaces;

public interface IAiCliIntegrationDataAccess
{
    Task<List<AiCliIntegration>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<AiCliIntegration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid workspaceId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(AiCliIntegration integration, CancellationToken cancellationToken = default);
    Task UpdateAsync(AiCliIntegration integration, CancellationToken cancellationToken = default);
    Task DeleteAsync(AiCliIntegration integration, CancellationToken cancellationToken = default);
}
