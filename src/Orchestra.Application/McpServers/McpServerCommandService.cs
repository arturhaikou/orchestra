using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Application.McpServers;

public sealed class McpServerCommandService : IMcpServerCommandService
{
    private readonly IWorkspaceAuthorizationService _authService;
    private readonly IMcpServerDataAccess _dataAccess;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly IMcpServerImpactCounter _impactCounter;

    public McpServerCommandService(
        IWorkspaceAuthorizationService authService,
        IMcpServerDataAccess dataAccess,
        ICredentialEncryptionService encryptionService,
        IMcpServerImpactCounter impactCounter)
    {
        _authService = authService;
        _dataAccess = dataAccess;
        _encryptionService = encryptionService;
        _impactCounter = impactCounter;
    }

    public async Task<McpServerListItemDto> CreateAsync(
        Guid userId,
        SaveMcpServerRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, request.WorkspaceId, cancellationToken);
        await EnsureNameIsUniqueAsync(request.WorkspaceId, request.Name, excludeId: null, cancellationToken);
        var server = BuildNewServer(request);
        await _dataAccess.AddAsync(server, cancellationToken);
        return MapToListItemDto(server);
    }

    public async Task<McpServerListItemDto> UpdateAsync(
        Guid userId,
        Guid serverId,
        PatchMcpServerRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, request.WorkspaceId, cancellationToken);
        var server = await LoadServerInWorkspaceAsync(serverId, request.WorkspaceId, cancellationToken);
        await EnsureNameIsUniqueAsync(request.WorkspaceId, request.Name, excludeId: serverId, cancellationToken);
        ApplyPatch(server, request);
        await _dataAccess.UpdateAsync(server, cancellationToken);
        return MapToListItemDto(server);
    }

    public async Task<DeleteMcpServerResponseDto> DeleteAsync(
        Guid userId,
        Guid serverId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);
        var server = await LoadServerInWorkspaceAsync(serverId, workspaceId, cancellationToken);
        var affectedAgents = await _impactCounter.CountImpactedAgentsAsync(serverId, cancellationToken);
        await _dataAccess.DeleteAsync(server, cancellationToken);
        return new DeleteMcpServerResponseDto(affectedAgents);
    }

    private async Task EnsureNameIsUniqueAsync(
        Guid workspaceId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var exists = await _dataAccess.ExistsByNameAsync(workspaceId, name, excludeId, cancellationToken);
        if (exists)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = [$"An MCP server named '{name}' already exists in this workspace."]
            });
    }

    private McpServer BuildNewServer(SaveMcpServerRequest request)
    {
        var transportType = Enum.Parse<McpTransportType>(request.TransportType, ignoreCase: true);
        return transportType == McpTransportType.HTTP
            ? BuildHttpServer(request)
            : BuildStdioServer(request);
    }

    private McpServer BuildHttpServer(SaveMcpServerRequest request)
    {
        var http = request.Http ?? throw new ArgumentException("HTTP fields are required for HTTP transport.");
        var authType = Enum.Parse<McpAuthType>(http.AuthType, ignoreCase: true);
        var encryptedKey = http.ApiKey is not null ? _encryptionService.Encrypt(http.ApiKey) : null;
        return McpServer.CreateHttp(request.WorkspaceId, request.Name, http.Url, authType, encryptedKey);
    }

    private McpServer BuildStdioServer(SaveMcpServerRequest request)
    {
        var stdio = request.Stdio ?? throw new ArgumentException("STDIO fields are required for STDIO transport.");
        var args = stdio.Args is { Length: > 0 } ? string.Join(" ", stdio.Args) : null;
        var encryptedEnvVars = BuildEncryptedEnvVars(stdio.EnvVars);
        return McpServer.CreateStdio(request.WorkspaceId, request.Name, stdio.Command, args, encryptedEnvVars);
    }

    private string? BuildEncryptedEnvVars(SaveEnvVar[]? envVars)
    {
        if (envVars is null or { Length: 0 }) return null;
        var pairs = envVars
            .Where(e => e.Value is not null)
            .ToDictionary(e => e.Key, e => _encryptionService.Encrypt(e.Value!));
        return System.Text.Json.JsonSerializer.Serialize(pairs);
    }

    private void ApplyPatch(McpServer server, PatchMcpServerRequest request)
    {
        var transportType = Enum.Parse<McpTransportType>(request.TransportType, ignoreCase: true);
        McpAuthType? authType = null;
        string? encryptedApiKey = null;
        string? encryptedEnvVars = null;

        if (transportType == McpTransportType.HTTP && request.Http is not null)
        {
            authType = Enum.Parse<McpAuthType>(request.Http.AuthType, ignoreCase: true);
            encryptedApiKey = request.Http.ApiKey is not null ? _encryptionService.Encrypt(request.Http.ApiKey) : null;
        }

        if (transportType == McpTransportType.STDIO && request.Stdio is not null)
            encryptedEnvVars = BuildPatchedEnvVars(request.Stdio.EnvVars);

        server.Update(
            name: request.Name,
            transportType: transportType,
            endpointUrl: request.Http?.Url,
            authType: authType,
            encryptedApiKey: encryptedApiKey,
            command: request.Stdio?.Command,
            arguments: request.Stdio?.Args is { Length: > 0 } ? string.Join(" ", request.Stdio.Args) : null,
            encryptedEnvironmentVariables: encryptedEnvVars);
    }

    private string? BuildPatchedEnvVars(PatchEnvVar[]? envVars)
    {
        if (envVars is null or { Length: 0 }) return null;
        var pairs = envVars
            .Where(e => e.Value is not null)
            .ToDictionary(e => e.Key, e => _encryptionService.Encrypt(e.Value!));
        return System.Text.Json.JsonSerializer.Serialize(pairs);
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

    private static McpServerListItemDto MapToListItemDto(McpServer server) =>
        new(
            Id: server.Id.ToString(),
            WorkspaceId: server.WorkspaceId.ToString(),
            Name: server.Name,
            ConnectionStatus: server.ConnectionStatus.ToString(),
            TransportType: server.TransportType.ToString(),
            EndpointUrl: server.EndpointUrl,
            Command: server.Command,
            CreatedAt: server.CreatedAt.ToString("O"));
}
