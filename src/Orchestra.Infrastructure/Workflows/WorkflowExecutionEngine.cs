using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.Models;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Workflows.DTOs;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Workflows;

public class WorkflowExecutionEngine : IWorkflowExecutionEngine
{
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid InProgressStatusId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid CompletedStatusId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly IWorkflowExecutionRepository _executionRepository;
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IAgentRuntimeService _agentRuntimeService;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IAgentContextBuilder _agentContextBuilder;
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IJobService _jobService;
    private readonly INotificationService _notificationService;
    private readonly ITicketIdParsingService _ticketIdParsingService;
    private readonly IWorkflowSystemToolRegistry _systemToolRegistry;
    private readonly ILogger<WorkflowExecutionEngine> _logger;

    public WorkflowExecutionEngine(
        IWorkflowExecutionRepository executionRepository,
        IWorkflowDefinitionRepository definitionRepository,
        IAgentRuntimeService agentRuntimeService,
        IAgentDataAccess agentDataAccess,
        IAgentContextBuilder agentContextBuilder,
        ITicketDataAccess ticketDataAccess,
        IJobService jobService,
        INotificationService notificationService,
        ITicketIdParsingService ticketIdParsingService,
        IWorkflowSystemToolRegistry systemToolRegistry,
        ILogger<WorkflowExecutionEngine> logger)
    {
        _executionRepository = executionRepository;
        _definitionRepository = definitionRepository;
        _agentRuntimeService = agentRuntimeService;
        _agentDataAccess = agentDataAccess;
        _agentContextBuilder = agentContextBuilder;
        _ticketDataAccess = ticketDataAccess;
        _jobService = jobService;
        _notificationService = notificationService;
        _ticketIdParsingService = ticketIdParsingService;
        _systemToolRegistry = systemToolRegistry;
        _logger = logger;
    }

    public async Task StartWorkflowAsync(
        Guid ticketId,
        Guid workflowDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            _logger.LogWarning("Workflow start failed: Ticket {TicketId} not found", ticketId);
            return;
        }

        var steps = await _definitionRepository.GetStepsByDefinitionIdAsync(workflowDefinitionId, cancellationToken);
        if (steps.Count == 0)
        {
            _logger.LogWarning("Workflow {WorkflowId} has no steps; skipping execution", workflowDefinitionId);
            return;
        }

        WorkflowExecution? execution = null;

        try
        {
            execution = WorkflowExecution.Create(workflowDefinitionId, ticketId, ticket.WorkspaceId);
            await _executionRepository.AddAsync(execution, cancellationToken);

            ticket.UpdateStatus(InProgressStatusId);
            await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

            var ticketIdForNotification = BuildCompositeTicketId(ticket);
            await _notificationService.NotifyTicketStatusChangedAsync(
                new TicketStatusChangedNotification(ticket.WorkspaceId, ticketIdForNotification, "In Progress", "To Do"),
                cancellationToken);

            var workflowDefinition = await _definitionRepository.GetByIdAsync(workflowDefinitionId, cancellationToken);
            var workflowName = workflowDefinition?.Name ?? "Workflow";

            var workflowJobId = await _jobService.CreateWorkflowJobAsync(
                ticket.WorkspaceId,
                workflowName,
                execution.Id,
                ticket.Id,
                ticket.Title,
                cancellationToken);

            execution.AssignWorkflowJob(workflowJobId);
            await _executionRepository.UpdateAsync(execution, cancellationToken);

            await _notificationService.NotifyWorkflowExecutionStatusChangedAsync(
                new WorkflowExecutionStatusChangedNotification(ticket.WorkspaceId, execution.Id, ticketId, WorkflowExecutionStatus.Running),
                cancellationToken);

            await ExecuteStepAsync(execution, steps, stepIndex: 0, previousOutput: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled error starting workflow {WorkflowId} for ticket {TicketId}",
                workflowDefinitionId,
                ticketId);

            if (execution is not null)
                await FailWorkflowAsync(execution, cancellationToken);
        }
    }

    public async Task HandleJobCompletedAsync(
        Guid jobId,
        string? output,
        CancellationToken cancellationToken = default)
    {
        var stepExecution = await _executionRepository.GetStepExecutionByJobIdAsync(jobId, cancellationToken);
        if (stepExecution is null) return;

        stepExecution.MarkCompleted(output);
        await _executionRepository.UpdateStepExecutionAsync(stepExecution, cancellationToken);

        var workflowExecution = await _executionRepository.GetByIdAsync(stepExecution.WorkflowExecutionId, cancellationToken);
        if (workflowExecution is null) return;

        var steps = await _definitionRepository.GetStepsByDefinitionIdAsync(workflowExecution.WorkflowDefinitionId, cancellationToken);

        await _notificationService.NotifyWorkflowStepCompletedAsync(
            new WorkflowStepCompletedNotification(
                workflowExecution.WorkspaceId,
                workflowExecution.Id,
                workflowExecution.TicketId,
                stepExecution.StepIndex,
                WorkflowExecutionStatus.Completed),
            cancellationToken);

        var nextStepIndex = stepExecution.StepIndex + 1;

        if (nextStepIndex >= steps.Count)
        {
            await CompleteWorkflowAsync(workflowExecution, cancellationToken);
            return;
        }

        workflowExecution.AdvanceToStep(nextStepIndex);
        await _executionRepository.UpdateAsync(workflowExecution, cancellationToken);

        await ExecuteStepAsync(workflowExecution, steps, nextStepIndex, previousOutput: output, cancellationToken);
    }

    public async Task HandleJobWaitingForInputAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var stepExecution = await _executionRepository.GetStepExecutionByJobIdAsync(jobId, cancellationToken);
        if (stepExecution is null) return;

        stepExecution.MarkWaitingForInput();
        await _executionRepository.UpdateStepExecutionAsync(stepExecution, cancellationToken);

        var workflowExecution = await _executionRepository.GetByIdAsync(stepExecution.WorkflowExecutionId, cancellationToken);
        if (workflowExecution is null) return;

        workflowExecution.MarkWaitingForInput();
        await _executionRepository.UpdateAsync(workflowExecution, cancellationToken);

        if (workflowExecution.WorkflowJobId.HasValue)
            await _jobService.UpdateJobStatusAsync(
                workflowExecution.WorkflowJobId.Value,
                JobStatus.WaitingForInput,
                cancellationToken: cancellationToken);

        await _notificationService.NotifyWorkflowExecutionStatusChangedAsync(
            new WorkflowExecutionStatusChangedNotification(
                workflowExecution.WorkspaceId,
                workflowExecution.Id,
                workflowExecution.TicketId,
                WorkflowExecutionStatus.WaitingForInput),
            cancellationToken);
    }

    public async Task HandleJobResumedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var stepExecution = await _executionRepository.GetStepExecutionByJobIdAsync(jobId, cancellationToken);
        if (stepExecution is null) return;

        var workflowExecution = await _executionRepository.GetByIdAsync(stepExecution.WorkflowExecutionId, cancellationToken);
        if (workflowExecution is null) return;

        workflowExecution.AdvanceToStep(workflowExecution.CurrentStepIndex);
        await _executionRepository.UpdateAsync(workflowExecution, cancellationToken);

        if (workflowExecution.WorkflowJobId.HasValue)
            await _jobService.UpdateJobStatusAsync(
                workflowExecution.WorkflowJobId.Value,
                JobStatus.Running,
                cancellationToken: cancellationToken);

        await _notificationService.NotifyWorkflowExecutionStatusChangedAsync(
            new WorkflowExecutionStatusChangedNotification(
                workflowExecution.WorkspaceId,
                workflowExecution.Id,
                workflowExecution.TicketId,
                WorkflowExecutionStatus.Running),
            cancellationToken);
    }

    private async Task ExecuteStepAsync(
        WorkflowExecution workflowExecution,
        List<WorkflowStep> steps,
        int stepIndex,
        string? previousOutput,
        CancellationToken cancellationToken)
    {
        var step = steps[stepIndex];

        var agent = await _agentDataAccess.GetByIdAsync(step.AgentId, cancellationToken);
        if (agent is null)
        {
            _logger.LogError(
                "Agent {AgentId} for workflow step {StepIndex} not found; failing workflow {ExecutionId}",
                step.AgentId, stepIndex, workflowExecution.Id);

            await FailWorkflowAsync(workflowExecution, cancellationToken);
            return;
        }

        var effectiveTicketId = workflowExecution.ActiveTicketId ?? workflowExecution.TicketId;
        var ticket = await _ticketDataAccess.GetTicketByIdAsync(effectiveTicketId, cancellationToken);
        if (ticket is null)
        {
            await FailWorkflowAsync(workflowExecution, cancellationToken);
            return;
        }

        var stepToolsMap = await _definitionRepository.GetSystemToolsByDefinitionIdAsync(
            workflowExecution.WorkflowDefinitionId, cancellationToken);
        var stepTools = stepToolsMap.GetValueOrDefault(step.Id, []);

        var stepExecution = WorkflowStepExecution.Create(workflowExecution.Id, stepIndex);
        await _executionRepository.AddStepExecutionAsync(stepExecution, cancellationToken);

        await _notificationService.NotifyWorkflowStepStartedAsync(
            new WorkflowStepStartedNotification(
                workflowExecution.WorkspaceId,
                workflowExecution.Id,
                workflowExecution.TicketId,
                stepIndex),
            cancellationToken);

        string? responseText = null;
        Guid? jobId = null;
        var stepFailed = false;

        try
        {
            var contextInput = await BuildStepContextAsync(
                ticket, agent, step, stepTools, previousOutput, cancellationToken);

            var jobContext = new JobContext(
                WorkspaceId: workflowExecution.WorkspaceId,
                AgentId: agent.Id,
                AgentName: agent.Name,
                TriggerType: JobTriggerType.Ticket,
                InitialPrompt: contextInput.TextPrompt,
                TicketId: ticket.Id,
                TicketTitle: ticket.Title,
                ParentJobId: workflowExecution.WorkflowJobId,
                WorkflowExecutionId: workflowExecution.Id,
                WorkflowSystemTools: stepTools);

            var (text, createdJobId) = await _agentRuntimeService.ExecuteAgentAsync(
                agent.Id,
                contextInput,
                agent.Model,
                agent.ProjectPrinciples,
                jobContext,
                cancellationToken);

            responseText = text;
            jobId = createdJobId;

            if (jobId.HasValue)
            {
                stepExecution.AssignJob(jobId.Value);
                await _executionRepository.UpdateStepExecutionAsync(stepExecution, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Step {StepIndex} of workflow execution {ExecutionId} failed",
                stepIndex,
                workflowExecution.Id);

            stepExecution.MarkFailed();
            await _executionRepository.UpdateStepExecutionAsync(stepExecution, cancellationToken);

            stepFailed = true;
            await FailWorkflowAsync(workflowExecution, cancellationToken);
        }

        if (!stepFailed && jobId.HasValue)
        {
            var finishedJob = await _jobService.GetJobAsync(jobId.Value, cancellationToken);
            if (finishedJob?.Status == JobStatus.WaitingForInput)
                await HandleJobWaitingForInputAsync(jobId.Value, cancellationToken);
            else
                await HandleJobCompletedAsync(jobId.Value, responseText, cancellationToken);
        }
    }

    private async Task<AgentContextInput> BuildStepContextAsync(
        Ticket ticket,
        Agent agent,
        WorkflowStep step,
        List<string> stepTools,
        string? previousOutput,
        CancellationToken cancellationToken)
    {
        var baseInput = await _agentContextBuilder.BuildAgentContextWithIntegrationsAsync(
            ticket, agent, cancellationToken);

        var text = step.PassPreviousOutput && !string.IsNullOrWhiteSpace(previousOutput)
            ? $"{baseInput.TextPrompt}\n\n[Previous Step Output]\n{previousOutput}"
            : baseInput.TextPrompt;

        text = AppendInstructionOverride(text, step.InstructionOverride);
        text = AppendSystemToolInstructions(text, stepTools);

        return new AgentContextInput(text, baseInput.Images);
    }

    private static string AppendInstructionOverride(string context, string? instructionOverride)
    {
        if (string.IsNullOrWhiteSpace(instructionOverride))
            return context;

        return $"{context}\n\n[Step Instructions]\n{instructionOverride}";
    }

    private static string AppendSystemToolInstructions(string context, List<string> stepTools)
    {
        if (!stepTools.Contains("switch_workflow_ticket"))
            return context;

        return context +
               "\n\n[Workflow Tool Instructions]\n" +
               "If you create an external ticket (Jira issue, GitHub issue, GitLab issue, etc.) during this step, " +
               "you MUST immediately call `switch_workflow_ticket` with the issue key and the integration ID. " +
               "This redirects all subsequent workflow steps to work on the newly created ticket instead of the current one.";
    }

    private async Task CompleteWorkflowAsync(WorkflowExecution workflowExecution, CancellationToken cancellationToken)
    {
        workflowExecution.MarkCompleted();
        await _executionRepository.UpdateAsync(workflowExecution, cancellationToken);

        // Update associated ticket status to Completed
        await UpdateTicketCompletedAsync(workflowExecution.TicketId, cancellationToken);

        if (workflowExecution.WorkflowJobId.HasValue)
            await _jobService.UpdateJobStatusAsync(
                workflowExecution.WorkflowJobId.Value,
                JobStatus.Completed,
                cancellationToken: cancellationToken);

        await _notificationService.NotifyWorkflowExecutionStatusChangedAsync(
            new WorkflowExecutionStatusChangedNotification(
                workflowExecution.WorkspaceId,
                workflowExecution.Id,
                workflowExecution.TicketId,
                WorkflowExecutionStatus.Completed),
            cancellationToken);

        _logger.LogInformation(
            "Workflow execution {ExecutionId} completed for ticket {TicketId}",
            workflowExecution.Id,
            workflowExecution.TicketId);
    }

    private async Task FailWorkflowAsync(WorkflowExecution workflowExecution, CancellationToken cancellationToken)
    {
        workflowExecution.MarkFailed();
        await _executionRepository.UpdateAsync(workflowExecution, cancellationToken);
        // Update associated ticket status to Completed
        await UpdateTicketCompletedAsync(workflowExecution.TicketId, cancellationToken);
        if (workflowExecution.WorkflowJobId.HasValue)
            await _jobService.UpdateJobStatusAsync(
                workflowExecution.WorkflowJobId.Value,
                JobStatus.Failed,
                errorMessage: "Workflow execution failed",
                cancellationToken: cancellationToken);

        await _notificationService.NotifyWorkflowExecutionStatusChangedAsync(
            new WorkflowExecutionStatusChangedNotification(
                workflowExecution.WorkspaceId,
                workflowExecution.Id,
                workflowExecution.TicketId,
                WorkflowExecutionStatus.Failed),
            cancellationToken);

        await RevertTicketToDoAsync(workflowExecution.TicketId, cancellationToken);
    }

    private async Task RevertTicketToDoAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
            if (ticket is null) return;

            ticket.UpdateStatus(ToDoStatusId);
            await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

            var ticketIdForNotification = BuildCompositeTicketId(ticket);
            await _notificationService.NotifyTicketStatusChangedAsync(
                new TicketStatusChangedNotification(ticket.WorkspaceId, ticketIdForNotification, "To Do", "In Progress"),
                cancellationToken);

            _logger.LogInformation(
                "Ticket {TicketId} reverted to To Do after workflow failure",
                ticketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to revert ticket {TicketId} to To Do status after workflow failure",
                ticketId);
        }
    }

    private async Task UpdateTicketCompletedAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
            if (ticket is null) return;

            ticket.UpdateStatus(CompletedStatusId);
            await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

            var ticketIdForNotification = BuildCompositeTicketId(ticket);
            await _notificationService.NotifyTicketStatusChangedAsync(
                new TicketStatusChangedNotification(ticket.WorkspaceId, ticketIdForNotification, "Completed", "In Progress"),
                cancellationToken);

            _logger.LogInformation(
                "Ticket {TicketId} updated to Completed after workflow execution",
                ticketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update ticket {TicketId} to Completed status after workflow execution",
                ticketId);
        }
    }

    private string BuildCompositeTicketId(Ticket ticket)
    {
        return (ticket.IntegrationId.HasValue && !string.IsNullOrEmpty(ticket.ExternalTicketId))
            ? _ticketIdParsingService.BuildCompositeId(ticket.IntegrationId.Value, ticket.ExternalTicketId)
            : ticket.Id.ToString();
    }
}
