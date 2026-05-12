using Orchestra.Application.AiCliIntegrations.DTOs;

namespace Orchestra.Application.AiCliIntegrations.Interfaces;

public interface IAiCliIntegrationQueryService
{
    Task<List<AiCliIntegrationDto>> GetListAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);
    Task<AiCliIntegrationDto> GetByIdAsync(Guid userId, Guid workspaceId, Guid integrationId, CancellationToken cancellationToken = default);
}
