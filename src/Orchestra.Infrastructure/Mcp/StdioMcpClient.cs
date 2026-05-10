using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Exceptions;

namespace Orchestra.Infrastructure.Mcp;

internal sealed class StdioMcpClient : IMcpClient, IAsyncDisposable
{
    private const int DiscoveryTimeoutSeconds = 30;

    private readonly string _command;
    private readonly string[] _arguments;
    private readonly Dictionary<string, string> _environmentVariables;
    private readonly ILogger<StdioMcpClient> _logger;

    private McpClient? _mcpClient;

    public StdioMcpClient(
        string command,
        string[] arguments,
        Dictionary<string, string> environmentVariables,
        ILogger<StdioMcpClient> logger)
    {
        _command = command;
        _arguments = arguments;
        _environmentVariables = environmentVariables;
        _logger = logger;
    }

    public async Task<IEnumerable<IMcpToolDescriptor>> ListToolsAsync(
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DiscoveryTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var client = await EnsureConnectedAsync(linkedCts.Token);
            var tools = await client.ListToolsAsync(cancellationToken: linkedCts.Token);
            return tools.Select(t => (IMcpToolDescriptor)new SdkMcpToolDescriptor(t));
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Stdio MCP process '{Command}' timed out after {Seconds}s.",
                _command, DiscoveryTimeoutSeconds);
            throw new DiscoveryTimeoutException(ex);
        }
        catch (Exception ex) when (ex is not DiscoveryTimeoutException)
        {
            _logger.LogWarning(ex, "Failed to launch or communicate with stdio MCP process '{Command}'.", _command);
            throw new ProcessLaunchException(_command, ex);
        }
    }

    private async Task<McpClient> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_mcpClient is not null)
            return _mcpClient;

        var transport = new StdioClientTransport(new()
        {
            Command = _command,
            Arguments = _arguments,
            EnvironmentVariables = _environmentVariables.Count > 0
                ? _environmentVariables
                : null
        });

        _mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        return _mcpClient;
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient is not null)
            await _mcpClient.DisposeAsync();
    }
}
