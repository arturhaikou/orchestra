using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tools.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Exceptions;
using Orchestra.Domain.Interfaces;
using Orchestra.Domain.ValueObjects;

namespace Orchestra.Application.Tools.Services;

public sealed class McpToolSeedingService : IMcpToolSeedingService
{
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly IMcpToolDiscoveryService _mcpToolDiscoveryService;
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess;
    private readonly IToolActionDataAccess _toolActionDataAccess;

    public McpToolSeedingService(
        IIntegrationDataAccess integrationDataAccess,
        ICredentialEncryptionService credentialEncryptionService,
        IMcpToolDiscoveryService mcpToolDiscoveryService,
        IToolCategoryDataAccess toolCategoryDataAccess,
        IToolActionDataAccess toolActionDataAccess)
    {
        _integrationDataAccess = integrationDataAccess ?? throw new ArgumentNullException(nameof(integrationDataAccess));
        _credentialEncryptionService = credentialEncryptionService ?? throw new ArgumentNullException(nameof(credentialEncryptionService));
        _mcpToolDiscoveryService = mcpToolDiscoveryService ?? throw new ArgumentNullException(nameof(mcpToolDiscoveryService));
        _toolCategoryDataAccess = toolCategoryDataAccess ?? throw new ArgumentNullException(nameof(toolCategoryDataAccess));
        _toolActionDataAccess = toolActionDataAccess ?? throw new ArgumentNullException(nameof(toolActionDataAccess));
    }

    public async Task<ToolDiscoveryResultDto> SeedToolsFromIntegrationAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        var integration = await LoadValidatedIntegrationAsync(integrationId, cancellationToken);
        var discoveryResult = await DiscoverToolsFromServerAsync(integration, cancellationToken);
        var category = await FindOrCreateCategoryAsync(integration, cancellationToken);
        var seededTools = await UpsertToolActionsAsync(category, integration.Id, discoveryResult.Tools, cancellationToken);
        return BuildResultDto(integration, category, seededTools);
    }

    private async Task<Domain.Entities.Integration> LoadValidatedIntegrationAsync(
        Guid integrationId,
        CancellationToken cancellationToken)
    {
        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new IntegrationNotFoundException(integrationId);

        if (integration.Provider == ProviderType.MCP_GENERIC)
            throw new ArgumentException(
                "MCP servers must be managed through the MCP Server settings",
                nameof(integrationId));

        return integration;
    }

    private Task<McpToolDiscoveryResult> DiscoverToolsFromServerAsync(
        Domain.Entities.Integration integration,
        CancellationToken cancellationToken)
    {
        return DiscoverHttpToolsAsync(integration, cancellationToken);
    }

    private Task<McpToolDiscoveryResult> DiscoverHttpToolsAsync(
        Domain.Entities.Integration integration,
        CancellationToken cancellationToken)
    {
        var decryptedApiKey = integration.EncryptedApiKey is not null
            ? _credentialEncryptionService.Decrypt(integration.EncryptedApiKey)
            : null;

        return _mcpToolDiscoveryService.DiscoverToolsAsync(
            integration.Url!,
            "API_KEY",
            decryptedApiKey,
            cancellationToken);
    }

    private async Task<ToolCategory> FindOrCreateCategoryAsync(
        Domain.Entities.Integration integration,
        CancellationToken cancellationToken)
    {
        var existing = await _toolCategoryDataAccess.FindByIntegrationIdAsync(integration.Id, cancellationToken);
        if (existing is not null)
            return existing;

        var newCategory = ToolCategory.CreateMcpCategory(
            integration.Name,
            $"Tools from {integration.Name} MCP server",
            integration.Provider,
            integration.Id);

        await _toolCategoryDataAccess.AddAsync(newCategory, cancellationToken);
        return newCategory;
    }

    private async Task<List<DiscoveredToolDto>> UpsertToolActionsAsync(
        ToolCategory category,
        Guid integrationId,
        IReadOnlyList<DiscoveredMcpTool> discoveredTools,
        CancellationToken cancellationToken)
    {
        var seeded = new List<DiscoveredToolDto>();
        foreach (var tool in discoveredTools)
        {
            var seededTool = await UpsertSingleToolActionAsync(category, integrationId, tool, cancellationToken);
            seeded.Add(seededTool);
        }
        return seeded;
    }

    private async Task<DiscoveredToolDto> UpsertSingleToolActionAsync(
        ToolCategory category,
        Guid integrationId,
        DiscoveredMcpTool tool,
        CancellationToken cancellationToken)
    {
        var existing = await _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(
            category.Id, tool.Name, cancellationToken);

        if (existing is not null)
        {
            existing.Update(tool.Name, tool.Description, tool.DangerLevel);
            existing.UpdateMcpSchema(tool.InputSchemaJson);
            await _toolActionDataAccess.UpdateAsync(existing, cancellationToken);
            return MapToDiscoveredToolDto(existing);
        }

        var newAction = ToolAction.CreateMcpTool(
            category.Id,
            integrationId,
            tool.Name,
            tool.Description,
            tool.Name,
            tool.DangerLevel,
            tool.InputSchemaJson,
            tool.Enabled);

        await _toolActionDataAccess.AddAsync(newAction, cancellationToken);
        return MapToDiscoveredToolDto(newAction);
    }

    private static DiscoveredToolDto MapToDiscoveredToolDto(ToolAction action) =>
        new(action.Id, action.Name, action.Description, action.DangerLevel.ToString(), action.IsEnabled, action.McpToolSchema);

    private static ToolDiscoveryResultDto BuildResultDto(
        Domain.Entities.Integration integration,
        ToolCategory category,
        List<DiscoveredToolDto> tools) =>
        new(
            IntegrationId: integration.Id,
            IntegrationName: integration.Name,
            CategoryId: category.Id,
            TotalToolCount: tools.Count,
            SafeCount: tools.Count(t => t.DangerLevel == DangerLevel.Safe.ToString()),
            ModerateCount: tools.Count(t => t.DangerLevel == DangerLevel.Moderate.ToString()),
            DestructiveCount: tools.Count(t => t.DangerLevel == DangerLevel.Destructive.ToString()),
            Tools: tools);
}
