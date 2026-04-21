using Microsoft.Agents.AI;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Service for creating and executing AI agents from database Agent entities.
/// </summary>
public class AgentRuntimeService : IAgentRuntimeService
{
    private readonly IChatClientResolver _chatClientResolver;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IToolRetrieverService _toolRetrieverService;

    public AgentRuntimeService(
        IChatClientResolver chatClientResolver,
        IAgentDataAccess agentDataAccess,
        IToolRetrieverService toolRetrieverService)
    {
        _chatClientResolver = chatClientResolver;
        _agentDataAccess = agentDataAccess;
        _toolRetrieverService = toolRetrieverService;
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteAgentAsync(
        Guid agentId,
        string contextPrompt,
        string? agentModel = null,
        string? projectPrinciples = null,
        CancellationToken cancellationToken = default)
    {
        // Load agent entity from database
        var agentEntity = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);
        if (agentEntity == null)
        {
            throw new InvalidOperationException($"Agent {agentId} not found");
        }

        // Resolve the LLM client for the workspace that owns this agent.
        // agentEntity.Model is the agent-configured model (non-null for configured agents).
        // The caller (AgentOrchestrationService) already validates agentEntity.Model
        // before reaching ExecuteAgentAsync, so null here is a programming error.
        var chatClient = await _chatClientResolver.ResolveAsync(
            agentEntity.WorkspaceId,
            agentEntity.Model ?? throw new InvalidOperationException(
                $"Agent {agentEntity.Id} has no model configured. Set Agent.Model before executing."),
            cancellationToken);

        // Get AIFunction instances for the agent's tools.
        // agentModel and projectPrinciples are captured in the review-action closure
        // inside ToolRetrieverService so the LLM never sees them as parameters.
        var aiFunctions = await _toolRetrieverService.GetAgentToolsAsync(
            agentId,
            agentModel,
            projectPrinciples,
            cancellationToken);

        // Create AIAgent using Microsoft Agent Framework with the resolved IChatClient.
        // The contextPrompt is expected to be fully enriched by the caller, including
        // any integration context blocks needed for external tool invocation.
        var agent = new ChatClientAgent(
            chatClient,
            instructions: agentEntity.CustomInstructions,
            name: agentEntity.Name,
            tools: aiFunctions.ToArray());

        // Execute agent and return response
        var response = await agent.RunAsync(contextPrompt, cancellationToken: cancellationToken);
        return response.Text ?? "Agent completed execution (no response text)";
    }
}
