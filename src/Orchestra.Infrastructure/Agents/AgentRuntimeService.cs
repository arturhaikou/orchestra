using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.AiCliIntegrations;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Service for creating and executing AI agents from database Agent entities.
/// </summary>
public class AgentRuntimeService : IAgentRuntimeService
{
    private readonly IChatClientResolver _chatClientResolver;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IChatAgentRunner _chatAgentRunner;
    private readonly IToolRetrieverService _toolRetrieverService;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;
    private readonly IAiCliClientFactory _cliClientFactory;
    private readonly IJobService _jobService;
    private readonly IJobStepWriter _jobStepWriter;

    public AgentRuntimeService(
        IChatClientResolver chatClientResolver,
        IAgentDataAccess agentDataAccess,
        IChatAgentRunner chatAgentRunner,
        IToolRetrieverService toolRetrieverService,
        IBuiltInAgentTemplateRegistry templateRegistry,
        IAiCliClientFactory cliClientFactory,
        IJobService jobService,
        IJobStepWriter jobStepWriter)
    {
        _chatClientResolver = chatClientResolver;
        _agentDataAccess = agentDataAccess;
        _chatAgentRunner = chatAgentRunner;
        _toolRetrieverService = toolRetrieverService;
        _templateRegistry = templateRegistry;
        _cliClientFactory = cliClientFactory;
        _jobService = jobService;
        _jobStepWriter = jobStepWriter;
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteAgentAsync(
        Guid agentId,
        string contextPrompt,
        string? agentModel = null,
        string? projectPrinciples = null,
        JobContext? jobContext = null,
        CancellationToken cancellationToken = default)
    {
        var agentEntity = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);
        if (agentEntity == null)
            throw new InvalidOperationException($"Agent {agentId} not found");

        Guid? jobId = null;
        if (jobContext is not null)
            jobId = await CreateAndStartJobAsync(jobContext, cancellationToken);

        try
        {
            string result;
            if (IsCliAgent(agentEntity))
                result = await ExecuteCliAgentAsync(agentEntity, contextPrompt, jobId, jobContext?.WorkspaceId, cancellationToken);
            else
                result = await ExecuteChatAgentAsync(
                    agentEntity,
                    agentId,
                    agentModel,
                    projectPrinciples,
                    contextPrompt,
                    jobId,
                    jobContext?.WorkspaceId,
                    cancellationToken);

            if (jobId.HasValue && jobContext is not null)
                await _jobService.UpdateJobStatusAsync(
                    jobId.Value,
                    JobStatus.Completed,
                    finalResponse: result,
                    cancellationToken: cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            if (jobId.HasValue)
                await _jobService.UpdateJobStatusAsync(
                    jobId.Value,
                    JobStatus.Failed,
                    errorMessage: ex.Message,
                    cancellationToken: cancellationToken);
            throw;
        }
    }

    private async Task<Guid> CreateAndStartJobAsync(
        JobContext jobContext,
        CancellationToken cancellationToken)
    {
        var jobId = await _jobService.CreateJobAsync(
            new CreateJobRequest(
                jobContext.WorkspaceId,
                jobContext.AgentId,
                jobContext.AgentName,
                jobContext.TriggerType,
                jobContext.InitialPrompt,
                jobContext.TicketId,
                jobContext.TicketTitle),
            cancellationToken);

        await _jobService.UpdateJobStatusAsync(
            jobId,
            JobStatus.Running,
            cancellationToken: cancellationToken);

        return jobId;
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
        Guid? jobId,
        Guid? workspaceId,
        CancellationToken cancellationToken)
    {
        if (agent.AiCliIntegrationId is null)
            throw new InvalidOperationException(
                $"CLI agent '{agent.Id}' has no AiCliIntegrationId configured. Re-deploy the template to bind a CLI integration.");

        var template = _templateRegistry.GetByIdentifier(agent.TemplateIdentifier!);
        var isReadOnly = template?.IsReadOnlyCli ?? true;

        if (jobId.HasValue && workspaceId.HasValue)
            return await ExecuteCliAgentWithTrackingAsync(agent, contextPrompt, isReadOnly, jobId.Value, workspaceId.Value, cancellationToken);

        return await ExecuteCliAgentWithoutTrackingAsync(agent, contextPrompt, isReadOnly, cancellationToken);
    }

    private async Task<string> ExecuteCliAgentWithTrackingAsync(
        Orchestra.Domain.Entities.Agent agent,
        string contextPrompt,
        bool isReadOnly,
        Guid jobId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var client = isReadOnly
            ? await _cliClientFactory.CreateReadOnlyClientAsync(agent.AiCliIntegrationId!.Value, agent.Model, agent.ReasoningEffort, cancellationToken)
            : await _cliClientFactory.CreateClientAsync(agent.AiCliIntegrationId!.Value, agent.Model, agent.ReasoningEffort, cancellationToken);

        await using var _ = client;

        await _jobStepWriter.WriteAsync(jobId, workspaceId, JobStepType.AgentStarted, cancellationToken: cancellationToken);
        try
        {
            var result = await client.RunWithTrackingAsync(
                contextPrompt, agent.CustomInstructions, agent.Name,
                _jobStepWriter, jobId, workspaceId, cancellationToken);

            await _jobStepWriter.WriteAsync(jobId, workspaceId, JobStepType.AgentCompleted, content: result, cancellationToken: cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await _jobStepWriter.WriteAsync(jobId, workspaceId, JobStepType.AgentFailed, content: ex.Message, isError: true, cancellationToken: cancellationToken);
            throw;
        }
    }

    private async Task<string> ExecuteCliAgentWithoutTrackingAsync(
        Orchestra.Domain.Entities.Agent agent,
        string contextPrompt,
        bool isReadOnly,
        CancellationToken cancellationToken)
    {
        if (isReadOnly)
        {
            await using var client = await _cliClientFactory.CreateReadOnlyClientAsync(
                agent.AiCliIntegrationId!.Value, agent.Model, agent.ReasoningEffort, cancellationToken);

            var aiAgent = client.AsReadOnlyAgent(agent.CustomInstructions, agent.Name);
            var response = await aiAgent.RunAsync(contextPrompt, cancellationToken: cancellationToken);
            return response.Text ?? "Agent completed execution (no response text)";
        }
        else
        {
            await using var client = await _cliClientFactory.CreateClientAsync(
                agent.AiCliIntegrationId!.Value, agent.Model, agent.ReasoningEffort, cancellationToken);

            var aiAgent = client.AsAgent(agent.CustomInstructions, agent.Name);
            var response = await aiAgent.RunAsync(contextPrompt, cancellationToken: cancellationToken);
            return response.Text ?? "Agent completed execution (no response text)";
        }
    }

    private async Task<string> ExecuteChatAgentAsync(
        Orchestra.Domain.Entities.Agent agentEntity,
        Guid agentId,
        string? agentModel,
        string? projectPrinciples,
        string contextPrompt,
        Guid? jobId,
        Guid? workspaceId,
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
            jobTracking: jobId.HasValue && workspaceId.HasValue
                ? new JobTrackingContext(_jobStepWriter, jobId.Value, workspaceId.Value)
                : null,
            cancellationToken);

        var jobTracking = jobId.HasValue && workspaceId.HasValue
            ? new JobTrackingContext(_jobStepWriter, jobId.Value, workspaceId.Value)
            : null;

        var result = await _chatAgentRunner.RunAsync(
            chatClient,
            agentEntity,
            aiFunctions.ToList(),
            contextPrompt,
            jobTracking,
            cancellationToken);

        return result ?? "Agent completed execution (no response text)";
    }
}
