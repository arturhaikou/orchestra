using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Skills.DTOs;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Resolves the AI provider, loads tools, and returns an <see cref="AgentAGUIContext"/>
/// for the given workspace agent, suitable for constructing an AG-UI streaming agent.
/// </summary>
public class AgentAGUIBuildService : IAgentAGUIBuildService
{
    private readonly IWorkspaceAuthorizationService _authorizationService;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IChatClientResolver _chatClientResolver;
    private readonly IToolRetrieverService _toolRetrieverService;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;
    private readonly IAgentSkillDataAccess _agentSkillDataAccess;

    public AgentAGUIBuildService(
        IWorkspaceAuthorizationService authorizationService,
        IAgentDataAccess agentDataAccess,
        IChatClientResolver chatClientResolver,
        IToolRetrieverService toolRetrieverService,
        IBuiltInAgentTemplateRegistry templateRegistry,
        IAgentSkillDataAccess agentSkillDataAccess)
    {
        _authorizationService = authorizationService;
        _agentDataAccess = agentDataAccess;
        _chatClientResolver = chatClientResolver;
        _toolRetrieverService = toolRetrieverService;
        _templateRegistry = templateRegistry;
        _agentSkillDataAccess = agentSkillDataAccess;
    }

    /// <inheritdoc/>
    public async Task<AgentAGUIContext?> BuildAGUIAgentContextAsync(
        Guid workspaceId,
        Guid agentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var agentEntity = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);
        if (agentEntity is null || agentEntity.WorkspaceId != workspaceId)
            return null;

        if (IsCliAgent(agentEntity))
            return BuildCliContext(agentEntity);

        return await BuildChatContextAsync(agentEntity, agentId, workspaceId, cancellationToken);
    }

    private bool IsCliAgent(Orchestra.Domain.Entities.Agent agent)
    {
        if (agent.TemplateIdentifier is null)
            return false;

        var template = _templateRegistry.GetByIdentifier(agent.TemplateIdentifier);
        return template?.IsCliAgent == true;
    }

    private static AgentAGUIContext BuildCliContext(Orchestra.Domain.Entities.Agent agent)
    {
        if (agent.AiCliIntegrationId is null)
            throw new InvalidOperationException(
                $"CLI agent '{agent.Id}' has no AiCliIntegrationId configured. Re-deploy the template to bind a CLI integration.");

        return new AgentAGUIContext(
            AgentName: agent.Name,
            Instructions: agent.CustomInstructions,
            IsCliAgent: true,
            ChatClient: null,
            Tools: [],
            AiCliIntegrationId: agent.AiCliIntegrationId);
    }

    private async Task<AgentAGUIContext> BuildChatContextAsync(
        Orchestra.Domain.Entities.Agent agent,
        Guid agentId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var chatClient = await _chatClientResolver.ResolveAsync(
            workspaceId,
            agent.Model ?? throw new InvalidOperationException(
                $"Agent {agentId} has no model configured. Assign a model before connecting via AG-UI."),
            cancellationToken);

        var tools = await _toolRetrieverService.GetAgentToolsAsync(
            agentId,
            agent.Model,
            agent.ProjectPrinciples,
            cancellationToken);

        var skillEntities = await _agentSkillDataAccess.GetSkillsByAgentIdAsync(agentId, cancellationToken);
        var skills = skillEntities
            .Select(s => new SkillDto(s.Id.ToString(), s.WorkspaceId.ToString(), s.Name, s.Description, s.Instructions, s.CreatedAt, s.UpdatedAt))
            .ToList();

        return new AgentAGUIContext(
            AgentName: agent.Name,
            Instructions: agent.CustomInstructions ?? agent.ProjectPrinciples,
            IsCliAgent: false,
            ChatClient: chatClient,
            Tools: tools,
            Skills: skills);
    }
}
