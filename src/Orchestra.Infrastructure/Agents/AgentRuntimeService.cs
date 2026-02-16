using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Service for creating and executing AI agents from database Agent entities.
/// </summary>
public class AgentRuntimeService : IAgentRuntimeService
{
    private readonly IChatClient _chatClient;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IToolRetrieverService _toolRetrieverService;

    public AgentRuntimeService(
        IChatClient chatClient,
        IAgentDataAccess agentDataAccess,
        IToolRetrieverService toolRetrieverService)
    {
        _chatClient = chatClient;
        _agentDataAccess = agentDataAccess;
        _toolRetrieverService = toolRetrieverService;
    }

    public async Task<string> ExecuteAgentAsync(
        Guid agentId,
        string contextPrompt,
        CancellationToken cancellationToken = default)
    {
        // Load agent entity from database
        var agentEntity = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);
        if (agentEntity == null)
        {
            throw new InvalidOperationException($"Agent {agentId} not found");
        }

        // Get AIFunction instances for the agent's tools
        var aiFunctions = await _toolRetrieverService.GetAgentToolsAsync(
            agentId,
            cancellationToken);

        // Create AIAgent using Microsoft Agent Framework with existing IChatClient
        var agent = _chatClient.AsAIAgent(
            instructions: agentEntity.CustomInstructions,
            name: agentEntity.Name,
            tools: aiFunctions.ToArray());

        // Execute agent and return response
        var response = await agent.RunAsync(contextPrompt, cancellationToken: cancellationToken);
        return response.Text ?? "Agent completed execution (no response text)";
    }
}
