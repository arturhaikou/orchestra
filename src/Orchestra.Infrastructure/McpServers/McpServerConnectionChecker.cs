using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Infrastructure.McpServers;

internal sealed class McpServerConnectionChecker : IMcpServerConnectionChecker
{
    private readonly IMcpToolDiscoveryService _discoveryService;
    private readonly ICredentialEncryptionService _encryptionService;

    public McpServerConnectionChecker(
        IMcpToolDiscoveryService discoveryService,
        ICredentialEncryptionService encryptionService)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public async Task<McpConnectionStatus> CheckAsync(
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ProbeServerAsync(server, cancellationToken);
            return McpConnectionStatus.Connected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return McpConnectionStatus.ConnectionFailed;
        }
    }

    private Task ProbeServerAsync(McpServer server, CancellationToken ct) =>
        server.TransportType == McpTransportType.HTTP
            ? ProbeHttpAsync(server, ct)
            : ProbeStdioAsync(server, ct);

    private Task ProbeHttpAsync(McpServer server, CancellationToken ct)
    {
        var apiKey = server.EncryptedApiKey is not null
            ? _encryptionService.Decrypt(server.EncryptedApiKey)
            : null;
        var authType = server.AuthType?.ToString() ?? "NONE";
        return _discoveryService.DiscoverToolsAsync(server.EndpointUrl!, authType, apiKey, ct);
    }

    private Task ProbeStdioAsync(McpServer server, CancellationToken ct)
    {
        var args = server.Arguments?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return _discoveryService.DiscoverStdioToolsAsync(server.Command!, args, null, ct);
    }
}
