using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Application.Agents.Services;

public class AgentOptionalToolService : IAgentOptionalToolService
{
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;
    private readonly IToolActionDataAccess _toolActionDataAccess;
    private readonly IIntegrationDataAccess _integrationDataAccess;

    public AgentOptionalToolService(
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IAgentDataAccess agentDataAccess,
        IBuiltInAgentTemplateRegistry templateRegistry,
        IAgentToolActionDataAccess agentToolActionDataAccess,
        IToolActionDataAccess toolActionDataAccess,
        IIntegrationDataAccess integrationDataAccess)
    {
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _agentDataAccess = agentDataAccess;
        _templateRegistry = templateRegistry;
        _agentToolActionDataAccess = agentToolActionDataAccess;
        _toolActionDataAccess = toolActionDataAccess;
        _integrationDataAccess = integrationDataAccess;
    }

    public async Task<List<string>> GetCurrentSelectionsAsync(Guid userId, Guid agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken)
            ?? throw new ArgumentException($"Agent '{agentId}' not found.");

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, agent.WorkspaceId, cancellationToken);

        var template = GetOptionalToolTemplate(agent.TemplateIdentifier);
        if (template is null)
            return new List<string>();

        var allOptionalMethodNames = GetAllOptionalMethodNames(template);
        var assignedIds = await _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, cancellationToken);

        if (assignedIds.Count == 0)
            return new List<string>();

        var assignedActions = await _toolActionDataAccess.GetEnabledByIdsAsync(assignedIds, cancellationToken);
        return assignedActions
            .Where(ta => allOptionalMethodNames.Contains(ta.Name))
            .Select(ta => ta.Name)
            .ToList();
    }

    public async Task SaveSelectionsAsync(Guid userId, Guid agentId, IReadOnlyList<string> methodNames, CancellationToken cancellationToken = default)
    {
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken)
            ?? throw new ArgumentException($"Agent '{agentId}' not found.");

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, agent.WorkspaceId, cancellationToken);

        var template = GetOptionalToolTemplate(agent.TemplateIdentifier)
            ?? throw new ArgumentException($"Agent '{agentId}' does not support optional tools.");

        var allOptionalMethodNames = GetAllOptionalMethodNames(template);
        var unknown = methodNames.Where(n => !allOptionalMethodNames.Contains(n)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Unknown optional tool method names: {string.Join(", ", unknown)}.");

        var assignedIds = await _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, cancellationToken);
        var currentActions = await _toolActionDataAccess.GetEnabledByIdsAsync(assignedIds, cancellationToken);
        var currentOptionalIds = currentActions
            .Where(ta => allOptionalMethodNames.Contains(ta.Name))
            .Select(ta => ta.Id)
            .ToHashSet();

        var targetActions = await _toolActionDataAccess.GetByNamesAsync(methodNames.ToList(), cancellationToken);
        var workspaceIntegrationIds = await GetWorkspaceIntegrationIdsAsync(agent.WorkspaceId, cancellationToken);
        var targetIds = targetActions
            .Where(ta => ta.IntegrationId == null || workspaceIntegrationIds.Contains(ta.IntegrationId.Value))
            .Select(ta => ta.Id)
            .ToHashSet();

        var toRemove = currentOptionalIds.Except(targetIds).ToList();
        var toAdd = targetIds.Except(currentOptionalIds).ToList();

        if (toRemove.Count > 0)
            await _agentToolActionDataAccess.RemoveToolActionsAsync(agentId, toRemove, cancellationToken);

        if (toAdd.Count > 0)
            await _agentToolActionDataAccess.AssignToolActionsAsync(agentId, toAdd, cancellationToken);
    }

    private BuiltInAgentTemplate? GetOptionalToolTemplate(string? templateIdentifier)
    {
        if (templateIdentifier is null)
            return null;

        var template = _templateRegistry.GetByIdentifier(templateIdentifier);
        if (template?.OptionalProviderToolMethodMap is null)
            return null;

        return template;
    }

    private static HashSet<string> GetAllOptionalMethodNames(BuiltInAgentTemplate template)
    {
        return template.OptionalProviderToolMethodMap!
            .Values
            .SelectMany(v => v)
            .ToHashSet();
    }

    private async Task<HashSet<Guid>> GetWorkspaceIntegrationIdsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        return integrations.Select(i => i.Id).ToHashSet();
    }
}
