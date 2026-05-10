namespace Orchestra.Application.Common.Interfaces;

public interface IMcpClientFactory : IAsyncDisposable
{
    Task<IMcpClient> CreateClientAsync(
        string endpointUrl,
        string mcpAuthType,
        string? apiKey,
        CancellationToken cancellationToken = default);

    Task<IMcpClient> GetOrCreateClientAsync(
        Guid integrationId,
        string mcpEndpointUrl,
        string? decryptedApiKey,
        CancellationToken cancellationToken = default);

    void DisposeClient(Guid integrationId);

    Task<IMcpClient> CreateStdioClientAsync(
        string command,
        string[]? arguments,
        Dictionary<string, string>? environmentVariables,
        CancellationToken cancellationToken = default);
}
