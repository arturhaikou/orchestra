using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
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
    private readonly IJobDataAccess _jobDataAccess;
    private readonly IChatAgentRunner _chatAgentRunner;
    private readonly IToolRetrieverService _toolRetrieverService;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;
    private readonly IAiCliClientFactory _cliClientFactory;
    private readonly IJobService _jobService;
    private readonly IJobStepWriter _jobStepWriter;
    private readonly IConversationSnapshotRepository _snapshotRepository;
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly INotificationService _notificationService;
    private readonly ITicketIdParsingService _ticketIdParsingService;

    // Status GUID from seeding
    private static readonly Guid CompletedStatusId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    public AgentRuntimeService(
        IChatClientResolver chatClientResolver,
        IAgentDataAccess agentDataAccess,
        IJobDataAccess jobDataAccess,
        IChatAgentRunner chatAgentRunner,
        IToolRetrieverService toolRetrieverService,
        IBuiltInAgentTemplateRegistry templateRegistry,
        IAiCliClientFactory cliClientFactory,
        IJobService jobService,
        IJobStepWriter jobStepWriter,
        IConversationSnapshotRepository snapshotRepository,
        ITicketDataAccess ticketDataAccess,
        INotificationService notificationService,
        ITicketIdParsingService ticketIdParsingService)
    {
        _chatClientResolver = chatClientResolver;
        _agentDataAccess = agentDataAccess;
        _jobDataAccess = jobDataAccess;
        _chatAgentRunner = chatAgentRunner;
        _toolRetrieverService = toolRetrieverService;
        _templateRegistry = templateRegistry;
        _cliClientFactory = cliClientFactory;
        _jobService = jobService;
        _jobStepWriter = jobStepWriter;
        _snapshotRepository = snapshotRepository;
        _ticketDataAccess = ticketDataAccess;
        _notificationService = notificationService;
        _ticketIdParsingService = ticketIdParsingService;
    }

    /// <inheritdoc/>
    public async Task<(string ResponseText, Guid? JobId)> ExecuteAgentAsync(
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
            JobTrackingContext? jobTracking = null;

            if (IsCliAgent(agentEntity))
                result = await ExecuteCliAgentAsync(agentEntity, contextPrompt, jobId, jobContext?.WorkspaceId, cancellationToken);
            else
            {
                jobTracking = jobId.HasValue && jobContext?.WorkspaceId is not null
                    ? new JobTrackingContext(_jobStepWriter, jobId.Value, jobContext.WorkspaceId)
                    : null;

                result = await ExecuteChatAgentAsync(
                    agentEntity,
                    agentId,
                    agentModel,
                    projectPrinciples,
                    contextPrompt,
                    jobTracking,
                    cancellationToken);
            }

            if (jobId.HasValue && jobContext is not null)
            {
                if (jobTracking?.SuspendedQuestionId is not null)
                    await _jobService.SuspendJobAsync(
                        jobId.Value, jobTracking.SuspendedQuestionId.Value, cancellationToken);
                else
                    await _jobService.UpdateJobStatusAsync(
                        jobId.Value,
                        JobStatus.Completed,
                        finalResponse: result,
                        cancellationToken: cancellationToken);
            }

            return (result, jobId);
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
                jobContext.TicketTitle,
                jobContext.ParentJobId,
                jobContext.WorkflowExecutionId),
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

        var customTools = (await _toolRetrieverService.GetAgentToolsAsync(
            agent.Id,
            cancellationToken: cancellationToken)).ToList();

        await _jobStepWriter.WriteAsync(jobId, workspaceId, JobStepType.AgentStarted, cancellationToken: cancellationToken);
        try
        {
            var result = await client.RunWithTrackingAsync(
                contextPrompt, agent.CustomInstructions, agent.Name,
                _jobStepWriter, jobId, workspaceId, customTools, cancellationToken);

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
        JobTrackingContext? jobTracking,
        CancellationToken cancellationToken)
    {
        var chatClient = await _chatClientResolver.ResolveAsync(
            agentEntity.WorkspaceId,
            agentEntity.Model ?? throw new InvalidOperationException(
                $"Agent {agentEntity.Id} has no model configured. Set Agent.Model before executing."),
            cancellationToken);

        var effectiveProjectPrinciples = projectPrinciples ?? agentEntity.ProjectPrinciples;

        if (jobTracking is not null)
        {
            effectiveProjectPrinciples = (effectiveProjectPrinciples ?? "") +
                "\n\nSYSTEM: If any tool returns a string starting with \"WAITING_FOR_USER_INPUT\", " +
                "return that string verbatim and stop calling further tools.";
        }

        var aiFunctions = await _toolRetrieverService.GetAgentToolsAsync(
            agentId,
            agentModel,
            effectiveProjectPrinciples,
            jobTracking: jobTracking,
            cancellationToken);

        var result = await _chatAgentRunner.RunAsync(
            chatClient,
            agentEntity,
            aiFunctions.ToList(),
            contextPrompt,
            jobTracking,
            cancellationToken: cancellationToken);

        return result ?? "Agent completed execution (no response text)";
    }

    /// <inheritdoc/>
    public async Task ExecuteRestoredAgentAsync(
        Guid jobId,
        Guid questionId,
        string answersJson,
        CancellationToken cancellationToken = default)
    {
        var job = await _jobDataAccess.GetByIdAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Job {jobId} not found.");
        
        var ticketId = job.TicketId;

        var agentEntity = await _agentDataAccess.GetByIdAsync(job.AgentId, cancellationToken)
            ?? throw new InvalidOperationException($"Agent {job.AgentId} not found.");

        var snapshot = await _snapshotRepository.GetByJobAndAgentAsync(jobId, job.AgentId, cancellationToken)
            ?? throw new InvalidOperationException($"No snapshot for job {jobId}.");

        var chatClient = await _chatClientResolver.ResolveAsync(
            agentEntity.WorkspaceId, agentEntity.Model!, cancellationToken);

        var maxSequence = await _jobDataAccess.GetMaxSequenceAsync(jobId, cancellationToken);
        _jobStepWriter.InitializeSequence(maxSequence);

        var jobTracking = new JobTrackingContext(_jobStepWriter, jobId, job.WorkspaceId);

        var aiFunctions = await _toolRetrieverService.GetAgentToolsAsync(
            job.AgentId, agentEntity.Model, agentEntity.ProjectPrinciples,
            jobTracking: jobTracking,
            cancellationToken);

#pragma warning disable MAAI001
        var agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = agentEntity.Name,
            ChatOptions = new ChatOptions
            {
                Instructions = agentEntity.CustomInstructions ?? agentEntity.ProjectPrinciples,
                Tools = aiFunctions.Cast<AITool>().ToList()
            }
        });

        var session = await agent.DeserializeSessionAsync(
            System.Text.Json.JsonDocument.Parse(snapshot.SerializedSessionJson).RootElement,
            cancellationToken: cancellationToken);
#pragma warning restore MAAI001

        var resumeMessage =
            $"The user has answered the pending question (QuestionId: {questionId}). " +
            $"Answers: {answersJson}. Continue where you left off.";

        await _jobService.UpdateJobStatusAsync(jobId, JobStatus.Running, cancellationToken: cancellationToken);

        try
        {
            var response = await _chatAgentRunner.RunAsync(
                chatClient,
                agentEntity,
                aiFunctions.ToList(),
                resumeMessage,
                jobTracking,
                session: session,
                cancellationToken: cancellationToken);

            if (jobTracking.SuspendedQuestionId is not null)
                await _jobService.SuspendJobAsync(
                    jobId, jobTracking.SuspendedQuestionId.Value, cancellationToken);
            else
            {
                await _jobService.UpdateJobStatusAsync(
                    jobId, JobStatus.Completed, finalResponse: response, cancellationToken: cancellationToken);

                // Update ticket to Completed only for standalone (non-workflow) jobs.
                // Workflow step jobs defer ticket lifecycle to the workflow engine.
                if (ticketId.HasValue && !job.WorkflowExecutionId.HasValue)
                {
                    var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId.Value, cancellationToken);
                    if (ticket is not null)
                    {
                        ticket.UpdateStatus(CompletedStatusId);
                        await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

                        var ticketIdForNotification = BuildCompositeTicketId(ticket);
                        await _notificationService.NotifyTicketStatusChangedAsync(
                            new TicketStatusChangedNotification(
                                ticket.WorkspaceId,
                                ticketIdForNotification,
                                "Completed",
                                "In Progress"),
                            cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await _jobService.UpdateJobStatusAsync(
                jobId, JobStatus.Failed, errorMessage: ex.Message, cancellationToken: cancellationToken);
            throw;
        }
    }

    private string BuildCompositeTicketId(Ticket ticket)
    {
        return (ticket.IntegrationId.HasValue && !string.IsNullOrEmpty(ticket.ExternalTicketId))
            ? _ticketIdParsingService.BuildCompositeId(ticket.IntegrationId.Value, ticket.ExternalTicketId)
            : ticket.Id.ToString();
    }
}
