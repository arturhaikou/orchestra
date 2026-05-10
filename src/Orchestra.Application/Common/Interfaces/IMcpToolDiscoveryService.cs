using Orchestra.Application.Integrations.DTOs;
using Orchestra.Domain.ValueObjects;

namespace Orchestra.Application.Common.Interfaces;

public interface IMcpToolDiscoveryService
{
    Task<McpToolDiscoveryResult> DiscoverToolsAsync(
        string endpointUrl,
        string mcpAuthType,
        string? apiKey,
        CancellationToken cancellationToken = default);

    Task<McpToolDiscoveryResult> DiscoverStdioToolsAsync(
        string command,
        string[]? arguments,
        Dictionary<string, string>? environmentVariables,
        CancellationToken cancellationToken = default);

    Task<SyncToolsResultDto> SyncToolsAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeededToolSummary>> DiscoverAndSeedToolsAsync(
        McpHttpDiscoveryRequest request,
        Guid integrationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeededToolSummary>> DiscoverAndSeedStdioToolsAsync(
        McpStdioDiscoveryRequest request,
        Guid integrationId,
        CancellationToken cancellationToken = default);
}
