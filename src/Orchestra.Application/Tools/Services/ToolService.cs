using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tools.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Tools.Services;

public class ToolService : IToolService
{
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess;
    private readonly IToolActionDataAccess _toolActionDataAccess;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;

    public ToolService(
        IToolCategoryDataAccess toolCategoryDataAccess,
        IToolActionDataAccess toolActionDataAccess,
        IAgentToolActionDataAccess agentToolActionDataAccess,
        IIntegrationDataAccess integrationDataAccess,
        IAgentDataAccess agentDataAccess,
        IWorkspaceAuthorizationService workspaceAuthorizationService)
    {
        _toolCategoryDataAccess = toolCategoryDataAccess ?? throw new ArgumentNullException(nameof(toolCategoryDataAccess));
        _toolActionDataAccess = toolActionDataAccess ?? throw new ArgumentNullException(nameof(toolActionDataAccess));
        _agentToolActionDataAccess = agentToolActionDataAccess ?? throw new ArgumentNullException(nameof(agentToolActionDataAccess));
        _integrationDataAccess = integrationDataAccess ?? throw new ArgumentNullException(nameof(integrationDataAccess));
        _agentDataAccess = agentDataAccess ?? throw new ArgumentNullException(nameof(agentDataAccess));
        _workspaceAuthorizationService = workspaceAuthorizationService ?? throw new ArgumentNullException(nameof(workspaceAuthorizationService));
    }

    public async Task<List<ToolCategoryDto>> GetAvailableToolsAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var nativeCategories = await LoadNativeCategoriesAsync(integrations, cancellationToken);

        var activeCategories = nativeCategories.Where(c => c.IsActive).ToList();
        if (activeCategories.Count == 0)
            return new List<ToolCategoryDto>();

        var actions = await _toolActionDataAccess.GetByCategoryIdsAsync(
            activeCategories.Select(c => c.Id).ToList(), cancellationToken);

        return activeCategories.Select(c => MapCategoryToDto(c, actions)).ToList();
    }

    private async Task<List<ToolCategory>> LoadNativeCategoriesAsync(
        List<Integration> integrations,
        CancellationToken cancellationToken)
    {
        var providerTypes = integrations
            .Where(i => i.IsActive)
            .Select(i => i.Provider)
            .Distinct()
            .ToList();

        providerTypes.Add(ProviderType.INTERNAL);
        return await _toolCategoryDataAccess.GetByProviderTypesAsync(providerTypes, cancellationToken);
    }

    private static ToolCategoryDto MapCategoryToDto(
        ToolCategory category,
        List<ToolAction> allActions)
    {
        var categoryActions = allActions
            .Where(a => a.ToolCategoryId == category.Id)
            .Select(a => MapActionToDto(a))
            .ToList();

        return new ToolCategoryDto(
            Id: category.Id,
            Name: category.Name,
            Description: category.Description,
            ProviderType: category.ProviderType.ToString(),
            Actions: categoryActions,
            Source: "native",
            IntegrationId: category.IntegrationId,
            IsMcpCategory: false,
            TransportType: null,
            IntegrationConnected: null);
    }

    private static ToolActionDto MapActionToDto(ToolAction action, Integration? integration = null)
    {
        return new ToolActionDto(
            Id: action.Id,
            CategoryId: action.ToolCategoryId,
            Name: action.Name,
            Description: action.Description,
            DangerLevel: action.DangerLevel.ToString(),
            IsEnabled: action.IsEnabled,
            IsMcpTool: action.IsMcpTool,
            McpToolSchema: action.McpToolSchema,
            Source: action.IsMcpTool ? "mcp" : "native",
            IntegrationId: action.IntegrationId,
            Transport: null,
            IntegrationName: integration?.Name);
    }

    public async Task<List<ToolActionDto>> GetAgentToolActionsAsync(
        Guid userId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);

        if (agent == null)
        {
            throw new AgentNotFoundException(agentId);
        }

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId,
            agent.WorkspaceId,
            cancellationToken);

        var workspaceIntegrations = await _integrationDataAccess.GetByWorkspaceIdAsync(
            agent.WorkspaceId,
            cancellationToken) ?? new List<Integration>();

        var integrationById = workspaceIntegrations.ToDictionary(i => i.Id);

        var toolActionIds = await _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(
            agentId,
            cancellationToken);

        var toolActions = new List<ToolActionDto>();

        foreach (var toolActionId in toolActionIds)
        {
            var action = await _toolActionDataAccess.GetByIdAsync(toolActionId, cancellationToken);
            if (action is { IsActive: true })
            {
                var integration = action.IntegrationId.HasValue
                    ? integrationById.GetValueOrDefault(action.IntegrationId.Value)
                    : null;
                toolActions.Add(MapActionToDto(action, integration));
            }
        }

        return toolActions;
    }

    public async Task AssignToolActionsToAgentAsync(
        Guid userId,
        Guid agentId,
        List<Guid> toolActionIds,
        CancellationToken cancellationToken = default)
    {
        if (toolActionIds.Count == 0)
            throw new ArgumentException("At least one tool action ID is required.", nameof(toolActionIds));

        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken)
            ?? throw new AgentNotFoundException(agentId);

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, agent.WorkspaceId, cancellationToken);

        await ValidateToolActionIdsAsync(toolActionIds, cancellationToken);

        await _agentToolActionDataAccess.AssignToolActionsAsync(agentId, toolActionIds, cancellationToken);
    }

    private async Task ValidateToolActionIdsAsync(
        List<Guid> toolActionIds,
        CancellationToken cancellationToken)
    {
        var enabledActions = await _toolActionDataAccess.GetEnabledByIdsAsync(toolActionIds, cancellationToken);
        var enabledIds = enabledActions.Select(a => a.Id).ToHashSet();
        var missingId = toolActionIds.FirstOrDefault(id => !enabledIds.Contains(id));

        if (missingId != default)
            throw new ToolActionNotFoundException(missingId);
    }

    public async Task RemoveToolActionsFromAgentAsync(
        Guid userId,
        Guid agentId,
        List<Guid> toolActionIds,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);

        if (agent == null)
        {
            throw new AgentNotFoundException(agentId);
        }

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId,
            agent.WorkspaceId,
            cancellationToken);

        await _agentToolActionDataAccess.RemoveToolActionsAsync(
            agentId,
            toolActionIds,
            cancellationToken);
    }

    public async Task<ToolActionDetailDto> ToggleToolActionEnabledAsync(
        Guid userId,
        Guid toolActionId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var toolAction = await LoadToolActionOrThrowAsync(toolActionId, cancellationToken);
        await AuthorizeUserForToolActionAsync(userId, toolAction, cancellationToken);
        toolAction.SetEnabled(isEnabled);
        await _toolActionDataAccess.UpdateAsync(toolAction, cancellationToken);
        return MapToToolActionDetailDto(toolAction);
    }

    private async Task<ToolAction> LoadToolActionOrThrowAsync(
        Guid toolActionId,
        CancellationToken cancellationToken)
    {
        return await _toolActionDataAccess.GetByIdAsync(toolActionId, cancellationToken)
            ?? throw new ToolActionNotFoundException(toolActionId);
    }

    private async Task AuthorizeUserForToolActionAsync(
        Guid userId,
        ToolAction toolAction,
        CancellationToken cancellationToken)
    {
        var category = await _toolCategoryDataAccess.GetByIdAsync(toolAction.ToolCategoryId, cancellationToken);
        if (category?.IntegrationId is null)
            return;

        var integration = await _integrationDataAccess.GetByIdAsync(category.IntegrationId.Value, cancellationToken);
        if (integration is null)
            return;

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, integration.WorkspaceId, cancellationToken);
    }

    private static ToolActionDetailDto MapToToolActionDetailDto(ToolAction toolAction) =>
        new(
            Id: toolAction.Id,
            CategoryId: toolAction.ToolCategoryId,
            Name: toolAction.Name,
            Description: toolAction.Description,
            DangerLevel: toolAction.DangerLevel.ToString(),
            IsEnabled: toolAction.IsEnabled,
            IsMcpTool: toolAction.IsMcpTool,
            McpToolSchema: toolAction.McpToolSchema);
}