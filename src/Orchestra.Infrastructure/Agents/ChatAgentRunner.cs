using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Agents.Middleware;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Concrete implementation of IChatAgentRunner that consolidates shared agent
/// assembly and execution logic formerly duplicated in AgentRuntimeService
/// and ToolRetrieverService.
/// </summary>
public class ChatAgentRunner : IChatAgentRunner
{
    private readonly IAgentSkillDataAccess _agentSkillDataAccess;
    private readonly ILogger<ChatAgentRunner> _logger;

    public ChatAgentRunner(
        IAgentSkillDataAccess agentSkillDataAccess,
        ILogger<ChatAgentRunner> logger)
    {
        _agentSkillDataAccess = agentSkillDataAccess;
        _logger = logger;
    }

    public async Task<string?> RunAsync(
        IChatClient chatClient,
        Agent agentEntity,
        IList<AIFunction> tools,
        string message,
        JobTrackingContext? jobTracking,
        CancellationToken cancellationToken)
    {
        // Step 1: Load skills and build ChatClientAgentOptions
        var skills = await _agentSkillDataAccess.GetSkillsByAgentIdAsync(agentEntity.Id, cancellationToken);

        var chatOptions = new ChatOptions
        {
            Instructions = agentEntity.CustomInstructions ?? agentEntity.ProjectPrinciples,
            Tools = tools.Cast<AITool>().ToList()
        };

        var agentOptions = new ChatClientAgentOptions
        {
            Name = agentEntity.Name,
            ChatOptions = chatOptions
        };

        // Step 2: Attach skills as context providers if any exist
        if (skills.Count > 0)
        {
#pragma warning disable MAAI001
            var inlineSkills = skills
                .Select(s => new AgentInlineSkill(
                    new AgentSkillFrontmatter(s.Name, s.Description),
                    s.Instructions))
                .ToArray();

            var skillsProvider = new AgentSkillsProvider(inlineSkills);
#pragma warning restore MAAI001

            agentOptions.AIContextProviders = [skillsProvider];

            _logger.LogDebug(
                "Attached {SkillCount} skills to agent {AgentName} ({AgentId})",
                skills.Count, agentEntity.Name, agentEntity.Id);
        }

        // Step 3: Wrap chat client with tracking middleware if job tracking is enabled
        IChatClient effectiveChatClient = chatClient;
        if (jobTracking is not null)
        {
            var chatMiddleware = new JobChatClientMiddlewareHandler(
                jobTracking.StepWriter,
                jobTracking.JobId,
                jobTracking.WorkspaceId);

            effectiveChatClient = chatClient
                .AsBuilder()
                .Use(
                    getResponseFunc: (msgs, opts, inner, ct) => chatMiddleware.HandleAsync(msgs, opts, inner, ct),
                    getStreamingResponseFunc: null)
                .Build();
        }

        // Step 4: Create agent from wrapped chat client
        var agent = effectiveChatClient.AsAIAgent(agentOptions);

        // Step 5: If job tracking is enabled, wrap agent with run and function-calling middleware
        if (jobTracking is not null)
        {
            var runMiddleware = new JobRunMiddlewareHandler(
                jobTracking.StepWriter,
                jobTracking.JobId,
                jobTracking.WorkspaceId);

            var funcMiddleware = new JobFunctionCallingMiddlewareHandler(
                jobTracking.StepWriter,
                jobTracking.JobId,
                jobTracking.WorkspaceId);

            var agentWithMiddleware = agent
                .AsBuilder()
                .Use(
                    runFunc: (msgs, session, opts, inner, ct) =>
                        runMiddleware.HandleAsync(msgs, session, opts, inner, ct),
                    runStreamingFunc: null)
                .Use((a, ctx, next, ct) => funcMiddleware.HandleAsync(a, ctx, next, ct))
                .Build();

            var response = await agentWithMiddleware.RunAsync(message, cancellationToken: cancellationToken);
            return response.Text;
        }
        else
        {
            var response = await agent.RunAsync(message, cancellationToken: cancellationToken);
            return response.Text;
        }
    }
}
