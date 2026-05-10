using Orchestra.Application.Tools.DTOs;

namespace Orchestra.Application.Common.Interfaces;

public interface IMcpToolSeedingService
{
    Task<ToolDiscoveryResultDto> SeedToolsFromIntegrationAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default);
}
