using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tools.DTOs;
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
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId,
            workspaceId,
            cancellationToken);

        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(
            workspaceId,
            cancellationToken);

        var connectedIntegrations = integrations.Where(i => i.IsActive).ToList();

        var providerTypes = connectedIntegrations
            .Select(i => i.Provider)
            .Distinct()
            .ToList();

        providerTypes.Add(ProviderType.INTERNAL);

        var categories = await _toolCategoryDataAccess.GetByProviderTypesAsync(
            providerTypes,
            cancellationToken);

        var categoryIds = categories.Select(c => c.Id).ToList();

        if (categoryIds.Count == 0)
        {
            return new List<ToolCategoryDto>();
        }

        var actions = await _toolActionDataAccess.GetByCategoryIdsAsync(
            categoryIds,
            cancellationToken);

        return categories.Select(category => new ToolCategoryDto(
            Id: category.Id,
            Name: category.Name,
            Description: category.Description,
            ProviderType: category.ProviderType.ToString(),
            Actions: actions
                .Where(a => a.ToolCategoryId == category.Id)
                .Select(a => new ToolActionDto(
                    Id: a.Id,
                    CategoryId: a.ToolCategoryId,
                    Name: a.Name,
                    Description: a.Description,
                    DangerLevel: a.DangerLevel.ToString()
                ))
                .ToList()
        )).ToList();
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

        var toolActionIds = await _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(
            agentId,
            cancellationToken);

        var toolActions = new List<ToolActionDto>();

        foreach (var toolActionId in toolActionIds)
        {
            var action = await _toolActionDataAccess.GetByIdAsync(toolActionId, cancellationToken);
            if (action != null)
            {
                toolActions.Add(new ToolActionDto(
                    Id: action.Id,
                    CategoryId: action.ToolCategoryId,
                    Name: action.Name,
                    Description: action.Description,
                    DangerLevel: action.DangerLevel.ToString()
                ));
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
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);

        if (agent == null)
        {
            throw new AgentNotFoundException(agentId);
        }

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId,
            agent.WorkspaceId,
            cancellationToken);

        foreach (var toolActionId in toolActionIds)
        {
            var action = await _toolActionDataAccess.GetByIdAsync(toolActionId, cancellationToken);
            if (action == null)
            {
                throw new ToolActionNotFoundException(toolActionId);
            }
        }

        await _agentToolActionDataAccess.AssignToolActionsAsync(
            agentId,
            toolActionIds,
            cancellationToken);
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
}