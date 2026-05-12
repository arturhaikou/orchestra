using Microsoft.Agents.AI;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Infrastructure.AiCliIntegrations;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Service for creating and executing AI agents from database Agent entities.
/// </summary>
public class AgentRuntimeService : IAgentRuntimeService
{
    private readonly IChatClientResolver _chatClientResolver;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IToolRetrieverService _toolRetrieverService;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;
    private readonly IAiCliClientFactory _cliClientFactory;

    public AgentRuntimeService(
        IChatClientResolver chatClientResolver,
        IAgentDataAccess agentDataAccess,
        IToolRetrieverService toolRetrieverService,
        IBuiltInAgentTemplateRegistry templateRegistry,
        IAiCliClientFactory cliClientFactory)
    {
        _chatClientResolver = chatClientResolver;
        _agentDataAccess = agentDataAccess;
        _toolRetrieverService = toolRetrieverService;
        _templateRegistry = templateRegistry;
        _cliClientFactory = cliClientFactory;
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteAgentAsync(
        Guid agentId,
        string contextPrompt,
        string? agentModel = null,
        string? projectPrinciples = null,
        CancellationToken cancellationToken = default)
    {
        var agentEntity = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);
        if (agentEntity == null)
            throw new InvalidOperationException($"Agent {agentId} not found");

        if (IsCliAgent(agentEntity))
            return await ExecuteCliAgentAsync(agentEntity, contextPrompt, cancellationToken);

        return await ExecuteChatAgentAsync(agentEntity, agentId, agentModel, projectPrinciples, contextPrompt, cancellationToken);
    }

    private bool IsCliAgent(Orchestra.Domain.Entities.Agent agent)
    {
        if (agent.TemplateIdentifier is null)
            return false;

        var template = _templateRegistry.GetByIdentifier(agent.TemplateIdentifier);
        return template?.IsCliAgent == true;
    }

    private async Task<string> ExecuteCliAgentAsync(
        Orchestra.Domain.Entities.Agent agent,
        string contextPrompt,
        CancellationToken cancellationToken)
    {
        if (agent.AiCliIntegrationId is null)
            throw new InvalidOperationException(
                $"CLI agent '{agent.Id}' has no AiCliIntegrationId configured. Re-deploy the template to bind a CLI integration.");

        await using var client = await _cliClientFactory.CreateReadOnlyClientAsync(
            agent.AiCliIntegrationId.Value, cancellationToken);

        var aiAgent = client.AsReadOnlyAgent(agent.CustomInstructions);
        var response = await aiAgent.RunAsync(contextPrompt, cancellationToken: cancellationToken);
        return response.Text ?? "Agent completed execution (no response text)";
    }

    private async Task<string> ExecuteChatAgentAsync(
        Orchestra.Domain.Entities.Agent agentEntity,
        Guid agentId,
        string? agentModel,
        string? projectPrinciples,
        string contextPrompt,
        CancellationToken cancellationToken)
    {
        var chatClient = await _chatClientResolver.ResolveAsync(
            agentEntity.WorkspaceId,
            agentEntity.Model ?? throw new InvalidOperationException(
                $"Agent {agentEntity.Id} has no model configured. Set Agent.Model before executing."),
            cancellationToken);

        var aiFunctions = await _toolRetrieverService.GetAgentToolsAsync(
            agentId,
            agentModel,
            projectPrinciples,
            cancellationToken);

        var agent = new ChatClientAgent(
            chatClient,
            instructions: agentEntity.CustomInstructions,
            name: agentEntity.Name,
            tools: aiFunctions.ToArray());

        var response = await agent.RunAsync(contextPrompt, cancellationToken: cancellationToken);
        return response.Text ?? "Agent completed execution (no response text)";
    }
}
