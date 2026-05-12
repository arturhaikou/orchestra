namespace Orchestra.Infrastructure.AiCliIntegrations;

public interface IAiCliClientFactory
{
    Task<IAiCliClient> CreateClientAsync(Guid integrationId, CancellationToken cancellationToken = default);

    Task<IAiCliClient> CreateReadOnlyClientAsync(Guid integrationId, CancellationToken cancellationToken = default);
}
