using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Mcp;

internal sealed class SdkMcpClient : IMcpClient, IAsyncDisposable
{
    private readonly HttpClientTransportOptions _transportOptions;
    private readonly ILoggerFactory _loggerFactory;
    private McpClient? _inner;
    private HttpClientTransport? _transport;

    public SdkMcpClient(HttpClientTransportOptions transportOptions, ILoggerFactory loggerFactory)
    {
        _transportOptions = transportOptions;
        _loggerFactory = loggerFactory;
    }

    public async Task<IEnumerable<IMcpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var client = await EnsureConnectedAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        return tools.Select(t => (IMcpToolDescriptor)new SdkMcpToolDescriptor(t));
    }

    private async Task<McpClient> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_inner is not null)
            return _inner;

        _transport = new HttpClientTransport(_transportOptions, _loggerFactory);
        _inner = await McpClient.CreateAsync(_transport, cancellationToken: cancellationToken);
        return _inner;
    }

    public async ValueTask DisposeAsync()
    {
        if (_inner is not null)
            await _inner.DisposeAsync();
        else if (_transport is not null)
            await _transport.DisposeAsync();
    }
}

