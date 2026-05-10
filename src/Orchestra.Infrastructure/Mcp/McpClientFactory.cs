using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Mcp;

internal sealed class McpClientFactory : IMcpClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpClientFactory> _logger;
    private readonly ConcurrentDictionary<Guid, IMcpClient> _clients = new();

    public McpClientFactory(ILoggerFactory loggerFactory, ILogger<McpClientFactory> logger)
    {
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public Task<IMcpClient> CreateClientAsync(
        string endpointUrl,
        string mcpAuthType,
        string? apiKey,
        CancellationToken cancellationToken = default)
    {
        ValidateHttpsUrl(endpointUrl);
        return Task.FromResult(BuildLazyClient(endpointUrl, mcpAuthType, apiKey));
    }

    public Task<IMcpClient> GetOrCreateClientAsync(
        Guid integrationId,
        string mcpEndpointUrl,
        string? decryptedApiKey,
        CancellationToken cancellationToken = default)
    {
        if (_clients.TryGetValue(integrationId, out var existingClient))
            return Task.FromResult(existingClient);

        var newClient = BuildLazyClient(
            mcpEndpointUrl,
            decryptedApiKey is not null ? "API_KEY" : "NONE",
            decryptedApiKey);

        _clients[integrationId] = newClient;
        return Task.FromResult(_clients[integrationId]);
    }

    private IMcpClient BuildLazyClient(string endpointUrl, string mcpAuthType, string? apiKey)
    {
        ValidateHttpsUrl(endpointUrl);

        var options = new HttpClientTransportOptions
        {
            Endpoint = new Uri(endpointUrl),
            Name = "MCP Server",
        };

        if (string.Equals(mcpAuthType, "API_KEY", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(apiKey))
        {
            options.AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {apiKey}"
            };
        }

        return new SdkMcpClient(options, _loggerFactory);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
            await DisposeClientSafely(client);

        _clients.Clear();
    }

    private static async ValueTask DisposeClientSafely(IMcpClient client)
    {
        try
        {
            if (client is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (client is IDisposable disposable)
                disposable.Dispose();
        }
        catch (Exception)
        {
        }
    }

    public void DisposeClient(Guid integrationId)
    {
        if (!_clients.TryRemove(integrationId, out var client))
        {
            _logger.LogDebug("No MCP client found in pool for integration {IntegrationId}. Nothing to dispose.", integrationId);
            return;
        }

        DisposeClientSafelySync(client, integrationId);
    }

    private void DisposeClientSafelySync(IMcpClient client, Guid integrationId)
    {
        try
        {
            if (client is IAsyncDisposable asyncDisposable)
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            else if (client is IDisposable disposable)
                disposable.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to dispose MCP client for integration {IntegrationId}. Connection may leak until process restart.",
                integrationId);
        }
    }

    public Task<IMcpClient> CreateStdioClientAsync(
        string command,
        string[]? arguments,
        Dictionary<string, string>? environmentVariables,
        CancellationToken cancellationToken = default)
    {
        var logger = _loggerFactory.CreateLogger<StdioMcpClient>();

        IMcpClient client = new StdioMcpClient(
            command,
            arguments ?? [],
            environmentVariables ?? new Dictionary<string, string>(),
            logger);

        return Task.FromResult(client);
    }

    private static void ValidateHttpsUrl(string endpointUrl)
    {
        if (!endpointUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MCP endpoint must use HTTPS.", nameof(endpointUrl));
    }
}
