using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.McpServers;

public sealed class McpServerQueryService : IMcpServerQueryService
{
    private readonly IWorkspaceAuthorizationService _authService;
    private readonly IMcpServerDataAccess _dataAccess;
    private readonly IMcpServerConnectionChecker _connectionChecker;

    public McpServerQueryService(
        IWorkspaceAuthorizationService authService,
        IMcpServerDataAccess dataAccess,
        IMcpServerConnectionChecker connectionChecker)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _dataAccess = dataAccess ?? throw new ArgumentNullException(nameof(dataAccess));
        _connectionChecker = connectionChecker ?? throw new ArgumentNullException(nameof(connectionChecker));
    }

    public async Task<List<McpServerListItemDto>> GetListAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);
        var servers = await _dataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);

        if (servers.Count == 0)
            return new List<McpServerListItemDto>();

        var liveStatuses = await Task.WhenAll(
            servers.Select(s => _connectionChecker.CheckAsync(s, cancellationToken)));

        return servers
            .Select((server, i) => MapToListItemDto(server, liveStatuses[i]))
            .ToList();
    }

    public async Task<GetMcpServerByIdResponseDto> GetByIdAsync(
        Guid userId,
        Guid workspaceId,
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);
        var server = await LoadServerInWorkspaceAsync(serverId, workspaceId, cancellationToken);
        return MapToDetailDto(server);
    }

    private async Task<McpServer> LoadServerInWorkspaceAsync(
        Guid serverId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var server = await _dataAccess.GetByIdAsync(serverId, cancellationToken)
            ?? throw new ArgumentException($"MCP server '{serverId}' was not found.", nameof(serverId));

        if (server.WorkspaceId != workspaceId)
            throw new WorkspaceAccessDeniedException(Guid.Empty, workspaceId,
                $"MCP server '{serverId}' does not belong to workspace '{workspaceId}'.");

        return server;
    }

    private static McpServerListItemDto MapToListItemDto(McpServer server, McpConnectionStatus liveStatus) =>
        new(
            Id: server.Id.ToString(),
            WorkspaceId: server.WorkspaceId.ToString(),
            Name: server.Name,
            ConnectionStatus: liveStatus.ToString(),
            TransportType: server.TransportType.ToString(),
            EndpointUrl: server.EndpointUrl,
            Command: server.Command,
            CreatedAt: server.CreatedAt.ToString("O"));

    private static GetMcpServerByIdResponseDto MapToDetailDto(McpServer server)
    {
        var args = server.Arguments?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var envVarKeys = DeserialiseEnvVarKeys(server.EncryptedEnvironmentVariables);
        var hasApiKey = server.EncryptedApiKey is not null;

        return new GetMcpServerByIdResponseDto(
            Id: server.Id.ToString(),
            WorkspaceId: server.WorkspaceId.ToString(),
            Name: server.Name,
            TransportType: server.TransportType.ToString(),
            ConnectionStatus: server.ConnectionStatus.ToString(),
            EndpointUrl: server.EndpointUrl,
            AuthType: hasApiKey ? null : server.AuthType?.ToString(),
            HasApiKey: hasApiKey,
            Command: server.Command,
            Args: args,
            EnvVarKeys: envVarKeys);
    }

    private static string[]? DeserialiseEnvVarKeys(string? encryptedEnvVars)
    {
        if (string.IsNullOrEmpty(encryptedEnvVars)) return null;

        try
        {
            var dict = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, string>>(encryptedEnvVars);
            return dict?.Keys.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
