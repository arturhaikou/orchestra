using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Exceptions;
using Orchestra.Domain.Interfaces;
using Orchestra.Domain.ValueObjects;

namespace Orchestra.Infrastructure.Mcp;

internal sealed class McpToolDiscoveryService : IMcpToolDiscoveryService
{
    private static readonly string[] DestructiveKeywords =
        ["delete", "remove", "destroy", "drop", "purge", "truncate", "erase"];

    private static readonly string[] ModerateKeywords =
        ["create", "update", "edit", "modify", "write", "post", "set", "put", "patch", "insert", "add"];

    private readonly IMcpClientFactory _clientFactory;
    private readonly IToolActionDataAccess _toolActionDataAccess;
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly IMcpServerDataAccess _mcpServerDataAccess;

    public McpToolDiscoveryService(IMcpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
        _toolActionDataAccess = null!;
        _toolCategoryDataAccess = null!;
        _integrationDataAccess = null!;
        _credentialEncryptionService = null!;
        _mcpServerDataAccess = null!;
    }

    public McpToolDiscoveryService(
        IMcpClientFactory clientFactory,
        IToolActionDataAccess toolActionDataAccess,
        IToolCategoryDataAccess toolCategoryDataAccess,
        IIntegrationDataAccess integrationDataAccess,
        ICredentialEncryptionService credentialEncryptionService,
        IMcpServerDataAccess mcpServerDataAccess)
    {
        _clientFactory = clientFactory;
        _toolActionDataAccess = toolActionDataAccess;
        _toolCategoryDataAccess = toolCategoryDataAccess;
        _integrationDataAccess = integrationDataAccess;
        _credentialEncryptionService = credentialEncryptionService;
        _mcpServerDataAccess = mcpServerDataAccess;
    }

    public async Task<McpToolDiscoveryResult> DiscoverToolsAsync(
        string endpointUrl,
        string mcpAuthType,
        string? apiKey,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var client = await _clientFactory.CreateClientAsync(endpointUrl, mcpAuthType, apiKey, linkedCts.Token);
        var rawTools = await FetchToolsFromServer(client, linkedCts.Token);
        return MapToDiscoveryResult(rawTools);
    }

    public async Task<McpToolDiscoveryResult> DiscoverStdioToolsAsync(
        string command,
        string[]? arguments,
        Dictionary<string, string>? environmentVariables,
        CancellationToken cancellationToken = default)
    {
        var client = await _clientFactory.CreateStdioClientAsync(
            command, arguments, environmentVariables, cancellationToken);
        try
        {
            var rawTools = await FetchToolsFromServer(client, cancellationToken);
            return MapToDiscoveryResult(rawTools);
        }
        finally
        {
            if (client is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
        }
    }

    private static async Task<IEnumerable<IMcpToolDescriptor>> FetchToolsFromServer(
        IMcpClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.ListToolsAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            throw new McpConnectionException(McpConnectionErrorCode.MCP_TIMEOUT, "MCP server connection timed out.", ex);
        }
        catch (HttpRequestException ex) when (IsAuthFailure(ex))
        {
            throw new McpConnectionException(McpConnectionErrorCode.MCP_AUTH_FAILED, "Authentication failed.", ex);
        }
        catch (Exception ex) when (ex is not McpConnectionException)
        {
            throw new McpConnectionException(McpConnectionErrorCode.MCP_UNREACHABLE, "MCP server is unreachable.", ex);
        }
    }

    private static McpToolDiscoveryResult MapToDiscoveryResult(IEnumerable<IMcpToolDescriptor> rawTools)
    {
        var discovered = rawTools.Select(ClassifyTool).ToList();
        return new McpToolDiscoveryResult(discovered);
    }

    private static DiscoveredMcpTool ClassifyTool(IMcpToolDescriptor tool)
    {
        var dangerLevel = ClassifyDangerLevel(tool.Name, tool.Description);
        var enabled = dangerLevel != DangerLevel.Destructive;
        return new DiscoveredMcpTool(tool.Name, tool.Description, dangerLevel, null, enabled);
    }

    private static DangerLevel ClassifyDangerLevel(string name, string? description)
    {
        var text = $"{name} {description ?? string.Empty}".ToLowerInvariant();

        if (ContainsAny(text, DestructiveKeywords))
            return DangerLevel.Destructive;

        if (ContainsAny(text, ModerateKeywords))
            return DangerLevel.Moderate;

        return DangerLevel.Safe;
    }

    private static bool ContainsAny(string text, string[] keywords) =>
        keywords.Any(kw => text.Contains(kw, StringComparison.Ordinal));

    private static bool IsAuthFailure(HttpRequestException ex) =>
        ex.Message.Contains("401", StringComparison.Ordinal) ||
        ex.Message.Contains("403", StringComparison.Ordinal) ||
        ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden;

    public async Task<SyncToolsResultDto> SyncToolsAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        var integration = await LoadIntegration(integrationId, cancellationToken);
        var category = await LoadCategory(integrationId, cancellationToken);
        var mcpServer = await LoadMcpServer(category, cancellationToken);
        var remoteTools = await FetchRemoteToolsForSync(mcpServer, cancellationToken);
        var existingTools = await _toolActionDataAccess.GetByIntegrationIdAsync(integrationId, cancellationToken);
        var syncStartedAt = DateTimeOffset.UtcNow;
        var summaries = await ApplyDiff(existingTools, remoteTools, category.Id, integrationId, syncStartedAt, cancellationToken);
        await PersistSyncResults(integration, syncStartedAt, cancellationToken);
        return BuildSyncResult(summaries, remoteTools.Count);
    }

    private async Task<Integration> LoadIntegration(Guid integrationId, CancellationToken cancellationToken)
    {
        return await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new ArgumentException("Integration not found.", nameof(integrationId));
    }

    private async Task<ToolCategory> LoadCategory(Guid integrationId, CancellationToken cancellationToken)
    {
        return await _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, cancellationToken)
            ?? throw new InvalidOperationException("Tool category not found for integration.");
    }

    private async Task<McpServer> LoadMcpServer(ToolCategory category, CancellationToken cancellationToken)
    {
        if (category.McpServerId is null)
            throw new InvalidOperationException("Integration is not linked to an MCP server.");
        return await _mcpServerDataAccess.GetByIdAsync(category.McpServerId.Value, cancellationToken)
            ?? throw new InvalidOperationException("MCP server not found.");
    }

    private async Task<List<IMcpToolDescriptor>> FetchRemoteToolsForSync(
        McpServer mcpServer,
        CancellationToken cancellationToken)
    {
        if (mcpServer.TransportType == McpTransportType.STDIO)
            return await FetchStdioToolsForSync(mcpServer, cancellationToken);

        return await FetchHttpToolsForSync(mcpServer, cancellationToken);
    }

    private async Task<List<IMcpToolDescriptor>> FetchHttpToolsForSync(
        McpServer mcpServer,
        CancellationToken cancellationToken)
    {
        var decryptedKey = DecryptApiKey(mcpServer);
        var client = await _clientFactory.GetOrCreateClientAsync(
            mcpServer.Id, mcpServer.EndpointUrl!, decryptedKey, cancellationToken);
        var rawTools = await FetchToolsFromServer(client, cancellationToken);
        return rawTools.ToList();
    }

    private async Task<List<IMcpToolDescriptor>> FetchStdioToolsForSync(
        McpServer mcpServer,
        CancellationToken cancellationToken)
    {
        var envVars = DecryptEnvironmentVariables(mcpServer);
        var args = DeserializeArguments(mcpServer.Arguments);
        var client = await _clientFactory.CreateStdioClientAsync(
            mcpServer.Command!, args, envVars, cancellationToken);
        try
        {
            var rawTools = await FetchToolsFromServer(client, cancellationToken);
            return rawTools.ToList();
        }
        finally
        {
            if (client is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
        }
    }

    private string? DecryptApiKey(McpServer mcpServer)
    {
        if (string.IsNullOrEmpty(mcpServer.EncryptedApiKey))
            return null;
        return _credentialEncryptionService.Decrypt(mcpServer.EncryptedApiKey);
    }

    private Dictionary<string, string>? DecryptEnvironmentVariables(McpServer mcpServer)
    {
        if (string.IsNullOrEmpty(mcpServer.EncryptedEnvironmentVariables))
            return null;

        var json = _credentialEncryptionService.Decrypt(mcpServer.EncryptedEnvironmentVariables);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    }

    private static string[]? DeserializeArguments(string? mcpArgumentsJson)
    {
        if (string.IsNullOrEmpty(mcpArgumentsJson))
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<string[]>(mcpArgumentsJson);
    }

    private async Task<List<(ToolAction?, IMcpToolDescriptor?, string)>> ApplyDiff(
        List<ToolAction> existing,
        List<IMcpToolDescriptor> remote,
        Guid categoryId,
        Guid integrationId,
        DateTimeOffset syncStartedAt,
        CancellationToken cancellationToken)
    {
        var remoteByName = remote.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var existingByName = existing.ToDictionary(t => t.MethodName, StringComparer.OrdinalIgnoreCase);
        var activeExistingByName = existing.Where(t => t.IsActive).ToDictionary(t => t.MethodName, StringComparer.OrdinalIgnoreCase);
        var summaries = new List<(ToolAction?, IMcpToolDescriptor?, string)>();
        summaries.AddRange(await ProcessNewTools(remoteByName, existingByName, categoryId, integrationId, syncStartedAt, cancellationToken));
        summaries.AddRange(await ProcessRemovedTools(activeExistingByName, remoteByName, syncStartedAt, cancellationToken));
        summaries.AddRange(await ProcessExistingTools(activeExistingByName, remoteByName, syncStartedAt, cancellationToken));
        return summaries;
    }

    private async Task<IEnumerable<(ToolAction?, IMcpToolDescriptor?, string)>> ProcessNewTools(
        Dictionary<string, IMcpToolDescriptor> remoteByName,
        Dictionary<string, ToolAction> existingByName,
        Guid categoryId,
        Guid integrationId,
        DateTimeOffset syncStartedAt,
        CancellationToken cancellationToken)
    {
        var reactivated = await ReactivatePreviouslyRemovedTools(remoteByName, existingByName, syncStartedAt, cancellationToken);
        var inserted = await InsertBrandNewTools(remoteByName, existingByName, categoryId, integrationId, syncStartedAt, cancellationToken);
        return reactivated.Concat(inserted);
    }

    private async Task<IEnumerable<(ToolAction?, IMcpToolDescriptor?, string)>> ReactivatePreviouslyRemovedTools(
        Dictionary<string, IMcpToolDescriptor> remoteByName,
        Dictionary<string, ToolAction> existingByName,
        DateTimeOffset syncStartedAt,
        CancellationToken cancellationToken)
    {
        var toReactivate = remoteByName.Values
            .Where(r => existingByName.TryGetValue(r.Name, out var t) && !t.IsActive)
            .Select(r => (Remote: r, Tool: existingByName[r.Name]))
            .ToList();

        foreach (var (remote, tool) in toReactivate)
        {
            tool.Reactivate(syncStartedAt);
            tool.UpdateFromSync(remote.Description, tool.McpToolSchema, syncStartedAt);
            await _toolActionDataAccess.UpdateAsync(tool, cancellationToken);
        }

        return toReactivate.Select(t => ((ToolAction?)t.Tool, (IMcpToolDescriptor?)t.Remote, "added"));
    }

    private async Task<IEnumerable<(ToolAction?, IMcpToolDescriptor?, string)>> InsertBrandNewTools(
        Dictionary<string, IMcpToolDescriptor> remoteByName,
        Dictionary<string, ToolAction> existingByName,
        Guid categoryId,
        Guid integrationId,
        DateTimeOffset syncStartedAt,
        CancellationToken cancellationToken)
    {
        var newTools = remoteByName.Values
            .Where(r => !existingByName.ContainsKey(r.Name))
            .Select(r => CreateSyncedToolAction(r, categoryId, integrationId, syncStartedAt))
            .ToList();

        if (newTools.Count > 0)
            await _toolActionDataAccess.AddRangeAsync(newTools, cancellationToken);

        return newTools.Select(ta => ((ToolAction?)ta, (IMcpToolDescriptor?)null, "added"));
    }

    private async Task<IEnumerable<(ToolAction?, IMcpToolDescriptor?, string)>> ProcessRemovedTools(
        Dictionary<string, ToolAction> existingByName,
        Dictionary<string, IMcpToolDescriptor> remoteByName,
        DateTimeOffset syncStartedAt,
        CancellationToken cancellationToken)
    {
        var removedTools = existingByName.Values
            .Where(e => !remoteByName.ContainsKey(e.MethodName))
            .ToList();
        foreach (var tool in removedTools)
        {
            tool.Deactivate(syncStartedAt);
            await _toolActionDataAccess.UpdateAsync(tool, cancellationToken);
        }
        return removedTools.Select(ta => ((ToolAction?)ta, (IMcpToolDescriptor?)null, "removed"));
    }

    private async Task<IEnumerable<(ToolAction?, IMcpToolDescriptor?, string)>> ProcessExistingTools(
        Dictionary<string, ToolAction> existingByName,
        Dictionary<string, IMcpToolDescriptor> remoteByName,
        DateTimeOffset syncStartedAt,
        CancellationToken cancellationToken)
    {
        var results = new List<(ToolAction?, IMcpToolDescriptor?, string)>();
        foreach (var (key, existing) in existingByName.Where(e => remoteByName.ContainsKey(e.Key)))
        {
            var remote = remoteByName[key];
            var hasChanges = existing.Description != remote.Description;
            existing.UpdateFromSync(remote.Description, existing.McpToolSchema, syncStartedAt);
            await _toolActionDataAccess.UpdateAsync(existing, cancellationToken);
            results.Add((existing, remote, hasChanges ? "updated" : "unchanged"));
        }
        return results;
    }

    private ToolAction CreateSyncedToolAction(
        IMcpToolDescriptor remote,
        Guid categoryId,
        Guid integrationId,
        DateTimeOffset syncStartedAt)
    {
        var dangerLevel = ClassifyDangerLevel(remote.Name, remote.Description);
        var enabled = dangerLevel != DangerLevel.Destructive;
        var toolAction = ToolAction.CreateMcpTool(
            categoryId, integrationId, remote.Name, remote.Description,
            remote.Name, dangerLevel, null, enabled);
        toolAction.UpdateFromSync(remote.Description, null, syncStartedAt);
        return toolAction;
    }

    private async Task PersistSyncResults(
        Integration integration,
        DateTimeOffset syncStartedAt,
        CancellationToken cancellationToken)
    {
        integration.UpdateLastSyncAt(syncStartedAt.DateTime);
        await _integrationDataAccess.UpdateAsync(integration, cancellationToken);
    }

    private static SyncToolsResultDto BuildSyncResult(
        List<(ToolAction? Existing, IMcpToolDescriptor? Remote, string Status)> summaries,
        int remoteCount)
    {
        var added = summaries.Count(s => s.Status == "added");
        var removed = summaries.Count(s => s.Status == "removed");
        var updated = summaries.Count(s => s.Status == "updated");
        var toolSummaries = summaries
            .Select(s => new SyncedToolSummaryDto(s.Existing?.Name ?? s.Remote?.Name ?? "unknown", s.Status))
            .ToList();
        return new SyncToolsResultDto(added, removed, updated, remoteCount, toolSummaries);
    }

    public async Task<IReadOnlyList<SeededToolSummary>> DiscoverAndSeedToolsAsync(
        McpHttpDiscoveryRequest request,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        var client = await _clientFactory.CreateClientAsync(
            request.EndpointUrl, request.AuthType, request.PlaintextApiKey, cancellationToken);

        var tools = (await client.ListToolsAsync(cancellationToken)).ToList();
        if (tools.Count == 0)
            return Array.Empty<SeededToolSummary>();

        var categoryId = await UpsertToolCategoryAsync(integrationId, cancellationToken);
        var existing = await _toolActionDataAccess.GetActiveByIntegrationIdAsync(integrationId, cancellationToken);
        var existingByName = existing.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var seeded = new List<SeededToolSummary>();
        foreach (var tool in tools)
        {
            var dangerLevel = ClassifyDangerLevel(tool.Name, tool.Description);
            var toolActionId = await UpsertToolActionAsync(
                categoryId, integrationId, tool, dangerLevel, existingByName, cancellationToken);
            seeded.Add(new SeededToolSummary(toolActionId, tool.Name, dangerLevel));
        }

        return seeded;
    }

    private async Task<Guid> UpsertToolCategoryAsync(Guid integrationId, CancellationToken cancellationToken)
    {
        var existing = await _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, cancellationToken);
        if (existing is not null)
            return existing.Id;

        var category = ToolCategory.CreateMcpCategory("Tools", string.Empty, ProviderType.MCP_GENERIC, integrationId);
        await _toolCategoryDataAccess.AddAsync(category, cancellationToken);
        return category.Id;
    }

    private async Task<Guid> UpsertToolActionAsync(
        Guid categoryId,
        Guid integrationId,
        IMcpToolDescriptor tool,
        DangerLevel dangerLevel,
        Dictionary<string, ToolAction> existingByName,
        CancellationToken cancellationToken)
    {
        if (existingByName.TryGetValue(tool.Name, out var existingAction))
        {
            existingAction.UpdateFromSync(tool.Description, existingAction.McpToolSchema, DateTimeOffset.UtcNow);
            await _toolActionDataAccess.UpdateAsync(existingAction, cancellationToken);
            return existingAction.Id;
        }

        var enabled = dangerLevel != DangerLevel.Destructive;
        var action = ToolAction.CreateMcpTool(
            categoryId, integrationId, tool.Name, tool.Description,
            tool.Name, dangerLevel, null, enabled);
        await _toolActionDataAccess.AddAsync(action, cancellationToken);
        return action.Id;
    }

    public async Task<IReadOnlyList<SeededToolSummary>> DiscoverAndSeedStdioToolsAsync(
        McpStdioDiscoveryRequest request,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        var client = await _clientFactory.CreateStdioClientAsync(
            request.Command, request.Arguments, request.PlaintextEnvironmentVariables, cancellationToken);
        await using var disposableClient = (IAsyncDisposable)client;

        var toolList = (await client.ListToolsAsync(cancellationToken)).ToList();
        if (toolList.Count == 0)
            return [];

        var category = await GetOrCreateStdioCategoryAsync(request.IntegrationName, integrationId, cancellationToken);
        return await SeedStdioToolActionsAsync(toolList, category.Id, integrationId, cancellationToken);
    }

    private async Task<ToolCategory> GetOrCreateStdioCategoryAsync(
        string integrationName,
        Guid integrationId,
        CancellationToken cancellationToken)
    {
        var existing = await _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, cancellationToken);
        if (existing is not null)
            return existing;

        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken);
        var provider = integration?.Provider ?? ProviderType.MCP_GENERIC;
        var category = ToolCategory.CreateMcpCategory(integrationName, string.Empty, provider, integrationId);
        await _toolCategoryDataAccess.AddAsync(category, cancellationToken);
        return category;
    }

    private async Task<IReadOnlyList<SeededToolSummary>> SeedStdioToolActionsAsync(
        IEnumerable<IMcpToolDescriptor> tools,
        Guid categoryId,
        Guid integrationId,
        CancellationToken cancellationToken)
    {
        var syncStartedAt = DateTimeOffset.UtcNow;
        var toolActions = tools
            .Select(t => CreateSyncedToolAction(t, categoryId, integrationId, syncStartedAt))
            .ToList();
        await _toolActionDataAccess.AddRangeAsync(toolActions, cancellationToken);
        return toolActions
            .Select(ta => new SeededToolSummary(ta.Id, ta.MethodName, ta.DangerLevel))
            .ToList();
    }
}
