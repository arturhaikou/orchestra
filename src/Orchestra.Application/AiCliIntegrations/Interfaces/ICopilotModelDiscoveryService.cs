using Orchestra.Application.AiCliIntegrations.DTOs;

namespace Orchestra.Application.AiCliIntegrations.Interfaces;

public interface ICopilotModelDiscoveryService
{
    Task<IReadOnlyList<ModelMetadataDto>> DiscoverModelsAsync(
        string? credential,
        bool useLoggedInUser,
        string workingDirectory,
        string? cliPath = null,
        CancellationToken cancellationToken = default);
}
