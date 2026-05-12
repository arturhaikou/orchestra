using Orchestra.Application.AiCliIntegrations.DTOs;

namespace Orchestra.Application.AiCliIntegrations.Interfaces;

public interface IAiCliIntegrationCommandService
{
    Task<AiCliIntegrationDto> CreateAsync(Guid userId, CreateAiCliIntegrationRequest request, CancellationToken cancellationToken = default);
    Task<AiCliIntegrationDto> UpdateAsync(Guid userId, Guid integrationId, UpdateAiCliIntegrationRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid integrationId, Guid workspaceId, CancellationToken cancellationToken = default);
}
