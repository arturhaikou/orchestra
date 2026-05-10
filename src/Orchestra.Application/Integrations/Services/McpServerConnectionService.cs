using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Domain.ValueObjects;

namespace Orchestra.Application.Integrations.Services;

public sealed class McpServerConnectionService : IMcpServerConnectionService
{
    private readonly IWorkspaceAuthorizationService _authService;
    private readonly IMcpToolDiscoveryService _discoveryService;

    public McpServerConnectionService(
        IWorkspaceAuthorizationService authService,
        IMcpToolDiscoveryService discoveryService)
    {
        _authService = authService;
        _discoveryService = discoveryService;
    }

    public async Task<ConnectMcpServerResponseDto> ConnectAsync(
        Guid userId,
        ConnectMcpServerRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, cancellationToken);

        ValidateRequest(request);

        var result = await DispatchProbeAsync(request, cancellationToken);

        return MapToResponseDto(result);
    }

    private Task<McpToolDiscoveryResult> DispatchProbeAsync(
        ConnectMcpServerRequest request,
        CancellationToken ct)
    {
        return request.TransportType.ToUpperInvariant() switch
        {
            "HTTP" => ProbeHttpAsync(request.Http!, ct),
            "STDIO" => ProbeStdioAsync(request.Stdio!, ct),
            _ => throw new ArgumentException(
                           $"Unsupported transportType: '{request.TransportType}'.")
        };
    }

    private Task<McpToolDiscoveryResult> ProbeHttpAsync(
        ConnectHttpFields http,
        CancellationToken ct)
    {
        var apiKey = http.AuthType.ToUpperInvariant() is "API_KEY" or "BEARER_TOKEN"
            ? http.ApiKey
            : null;

        return _discoveryService.DiscoverToolsAsync(
            http.Url, http.AuthType, apiKey, ct);
    }

    private Task<McpToolDiscoveryResult> ProbeStdioAsync(
        ConnectStdioFields stdio,
        CancellationToken ct)
    {
        var envDict = stdio.EnvVars is { Length: > 0 }
            ? stdio.EnvVars.ToDictionary(e => e.Key, e => e.Value)
            : null;

        return _discoveryService.DiscoverStdioToolsAsync(
            stdio.Command, stdio.Args, envDict, ct);
    }

    private static ConnectMcpServerResponseDto MapToResponseDto(McpToolDiscoveryResult result)
    {
        var tools = result.Tools
            .Select(t => new ToolPreviewDto(t.Name, t.Description))
            .ToList();

        return new ConnectMcpServerResponseDto(tools);
    }

    private static void ValidateRequest(ConnectMcpServerRequest request)
    {
        var transport = request.TransportType?.ToUpperInvariant();

        if (transport == "HTTP" && request.Http is null)
            throw new ArgumentException(
                "ConnectMcpServerRequest.Http must be provided when TransportType is 'HTTP'.");

        if (transport == "STDIO" && request.Stdio is null)
            throw new ArgumentException(
                "ConnectMcpServerRequest.Stdio must be provided when TransportType is 'STDIO'.");
    }
}
