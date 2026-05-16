namespace Orchestra.Infrastructure.AiCliIntegrations;

public interface IAiCliClientFactory
{
    Task<IAiCliClient> CreateClientAsync(
        Guid integrationId,
        string? modelId = null,
        string? reasoningEffort = null,
        CancellationToken cancellationToken = default);

    Task<IAiCliClient> CreateReadOnlyClientAsync(
        Guid integrationId,
        string? modelId = null,
        string? reasoningEffort = null,
        CancellationToken cancellationToken = default);
}
