using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.DTOs;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Application.McpServers;

public sealed class McpServerToolFetcher : IMcpServerToolFetcher
{
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    private readonly IMcpServerDataAccess _dataAccess;
    private readonly IMcpClientFactory _clientFactory;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly IWorkspaceAuthorizationService _authService;

    public McpServerToolFetcher(
        IMcpServerDataAccess dataAccess,
        IMcpClientFactory clientFactory,
        ICredentialEncryptionService encryptionService,
        IWorkspaceAuthorizationService authService)
    {
        _dataAccess = dataAccess;
        _clientFactory = clientFactory;
        _encryptionService = encryptionService;
        _authService = authService;
    }

    public async Task<McpToolFetchResult> FetchToolsAsync(
        Guid userId,
        Guid workspaceId,
        Guid mcpServerId,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);

        var server = await _dataAccess.GetByIdAsync(mcpServerId, cancellationToken)
            ?? throw new ArgumentException($"MCP server '{mcpServerId}' was not found.");

        if (server.WorkspaceId != workspaceId)
            throw new WorkspaceAccessDeniedException(userId, workspaceId);

        using var timeoutCts = new CancellationTokenSource(FetchTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            var client = await CreateClientAsync(server, linkedCts.Token);
            var rawTools = await client.ListToolsAsync(linkedCts.Token);
            var toolList = rawTools.ToList();

            if (toolList.Count == 0)
                return new McpToolFetchResult.Empty();

            var items = toolList.Select(t => new McpToolItem(
                t.Name,
                t.Description,
                McpToolDangerLevelClassifier.Classify(t.Name, t.Description)
            )).ToList();

            return new McpToolFetchResult.Success(items);
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolFetchResult.AuthFailed();
        }
        catch (OperationCanceledException ex)
        {
            return new McpToolFetchResult.Unreachable(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return new McpToolFetchResult.Unreachable(ex.Message);
        }
        catch (Exception ex) when (IsConnectivityException(ex))
        {
            return new McpToolFetchResult.Unreachable(ex.Message);
        }
    }

    private async Task<IMcpClient> CreateClientAsync(
        Orchestra.Domain.Entities.McpServer server,
        CancellationToken ct)
    {
        if (server.TransportType == McpTransportType.STDIO)
        {
            return await _clientFactory.CreateStdioClientAsync(
                server.Command!,
                server.Arguments?.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                DecryptEnvironmentVariables(server.EncryptedEnvironmentVariables),
                ct);
        }

        var apiKey = server.EncryptedApiKey is not null
            ? _encryptionService.Decrypt(server.EncryptedApiKey)
            : null;

        return await _clientFactory.CreateClientAsync(
            server.EndpointUrl!,
            server.AuthType?.ToString() ?? "NONE",
            apiKey,
            ct);
    }

    private Dictionary<string, string>? DecryptEnvironmentVariables(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;

        var decrypted = _encryptionService.Decrypt(encrypted);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);
    }

    private static bool IsConnectivityException(Exception ex) =>
        ex is System.Net.Sockets.SocketException
        || ex is System.IO.IOException
        || ex is TimeoutException;
}
