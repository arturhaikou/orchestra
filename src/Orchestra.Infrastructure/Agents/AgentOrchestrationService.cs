using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Models;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Service for orchestrating automated agent execution on tickets.
/// Manages the full lifecycle: status updates, context building, agent execution, and result storage.
/// </summary>
public class AgentOrchestrationService : IAgentOrchestrationService
{
    private readonly IAgentRuntimeService _agentRuntimeService;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IJobService _jobService;
    private readonly IAgentContextBuilder _agentContextBuilder;
    private readonly INotificationService _notificationService;
    private readonly IWorkflowExecutionEngine _workflowExecutionEngine;
    private readonly ITicketIdParsingService _ticketIdParsingService;
    private readonly ILogger<AgentOrchestrationService> _logger;

    // Status GUIDs from seeding
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid InProgressStatusId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid CompletedStatusId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    public AgentOrchestrationService(
        IAgentRuntimeService agentRuntimeService,
        IAgentDataAccess agentDataAccess,
        ITicketDataAccess ticketDataAccess,
        IJobService jobService,
        IAgentContextBuilder agentContextBuilder,
        INotificationService notificationService,
        IWorkflowExecutionEngine workflowExecutionEngine,
        ITicketIdParsingService ticketIdParsingService,
        ILogger<AgentOrchestrationService> logger)
    {
        _agentRuntimeService = agentRuntimeService;
        _agentDataAccess = agentDataAccess;
        _ticketDataAccess = ticketDataAccess;
        _jobService = jobService;
        _agentContextBuilder = agentContextBuilder;
        _notificationService = notificationService;
        _workflowExecutionEngine = workflowExecutionEngine;
        _ticketIdParsingService = ticketIdParsingService;
        _logger = logger;
    }

    public async Task<AgentExecutionResult> ExecuteAgentForTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        Ticket? ticket = null;
        Agent? agentEntity = null;

        try
        {
            // 1. Load ticket
            ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
            if (ticket == null)
            {
                _logger.LogWarning("Ticket {TicketId} not found", ticketId);
                return AgentExecutionResult.Failure("Ticket not found");
            }

            if (!ticket.AssignedAgentId.HasValue)
            {
                _logger.LogWarning("Ticket {TicketId} has no assigned agent", ticketId);
                return AgentExecutionResult.Failure("No agent assigned to ticket");
            }

            // 2. Load agent entity
            agentEntity = await _agentDataAccess.GetByIdAsync(
                ticket.AssignedAgentId.Value,
                cancellationToken);

            if (agentEntity == null)
            {
                _logger.LogWarning(
                    "Agent {AgentId} assigned to ticket {TicketId} not found",
                    ticket.AssignedAgentId.Value,
                    ticketId);
                return AgentExecutionResult.Failure("Assigned agent not found");
            }

            _logger.LogInformation(
                "Starting agent execution: Agent {AgentName} ({AgentId}) on Ticket {TicketId}",
                agentEntity.Name,
                agentEntity.Id,
                ticketId);

            // 3. Set agent status to Busy
            agentEntity.UpdateStatus(AgentStatus.Busy);
            await _agentDataAccess.UpdateAsync(agentEntity, cancellationToken);

            // 4. Initialize status for legacy tickets and set to InProgress
            // For legacy external tickets (StatusId=null), set to "To Do" first
            if (!ticket.StatusId.HasValue)
            {
                ticket.UpdateStatus(ToDoStatusId);
            }

            ticket.UpdateStatus(InProgressStatusId);
            await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

            var ticketIdForNotification = BuildCompositeTicketId(ticket);
            await _notificationService.NotifyTicketStatusChangedAsync(
                new TicketStatusChangedNotification(
                    ticket.WorkspaceId,
                    ticketIdForNotification,
                    "In Progress",
                    "To Do"),
                cancellationToken);

            // 5. Build context prompt with integrations (Phase 1: ticket, Phase 2: integrations, Phase 3: project principles)
            var contextInput = await _agentContextBuilder.BuildAgentContextWithIntegrationsAsync(
                ticket,
                agentEntity,
                cancellationToken);

            _logger.LogDebug(
                "Built context prompt for ticket {TicketId}: {PromptLength} characters, {ImageCount} image(s)",
                ticketId,
                contextInput.TextPrompt.Length,
                contextInput.Images.Count);

            // 6. Execute AI agent — resolve model at execution time from the agent entity.
            // agentEntity.Model is non-null when the agent has a configured model override;
            // null causes AgentRuntimeService to fall back to the system-configured default.
            _logger.LogInformation(
                "Executing agent {AgentName} with model {AgentModel}",
                agentEntity.Name,
                agentEntity.Model ?? "<system default>");

            var jobContext = new JobContext(
                WorkspaceId: ticket.WorkspaceId,
                AgentId: agentEntity.Id,
                AgentName: agentEntity.Name,
                TriggerType: JobTriggerType.Ticket,
                InitialPrompt: contextInput.TextPrompt,
                TicketId: ticket.Id,
                TicketTitle: ticket.Title);

            var (responseText, jobId) = await _agentRuntimeService.ExecuteAgentAsync(
                agentEntity.Id,
                contextInput,
                agentEntity.Model,
                agentEntity.ProjectPrinciples,
                jobContext,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Agent execution completed successfully for ticket {TicketId}",
                ticketId);

            // 7. Update ticket status - but only to Completed if the job is not suspended waiting for input
            bool shouldMarkCompleted = true;
            if (jobId.HasValue)
            {
                var job = await _jobService.GetJobAsync(jobId.Value, cancellationToken);
                if (job?.Status == JobStatus.Cancelled)
                {
                    _logger.LogInformation(
                        "Job {JobId} was cancelled; leaving ticket {TicketId} status unchanged",
                        jobId.Value,
                        ticketId);
                    return AgentExecutionResult.Failure("Cancelled");
                }
                if (job?.Status == JobStatus.WaitingForInput)
                {
                    shouldMarkCompleted = false;
                    _logger.LogInformation(
                        "Job {JobId} is waiting for user input; keeping ticket {TicketId} in In Progress status",
                        jobId.Value,
                        ticketId);
                }
            }

            if (shouldMarkCompleted)
            {
                ticket.UpdateStatus(CompletedStatusId);
                await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

                var completedTicketIdForNotification = BuildCompositeTicketId(ticket);
                await _notificationService.NotifyTicketStatusChangedAsync(
                    new TicketStatusChangedNotification(
                        ticket.WorkspaceId,
                        completedTicketIdForNotification,
                        "Completed",
                        "In Progress"),
                    cancellationToken);
            }

            // Notify workflow engine of job outcome
            if (jobId.HasValue)
            {
                if (!shouldMarkCompleted)
                {
                    await _workflowExecutionEngine.HandleJobWaitingForInputAsync(jobId.Value, cancellationToken);
                }
                else
                {
                    await _workflowExecutionEngine.HandleJobCompletedAsync(jobId.Value, responseText, cancellationToken);
                }
            }

            // 8. Log completion
            _logger.LogInformation(
                "Agent {AgentName} completed execution on ticket {TicketId}",
                agentEntity.Name,
                ticketId);

            var successResult = AgentExecutionResult.Success(responseText);

            await DispatchNotificationAsync(
                ticket, agentEntity, successResult, cancellationToken);

            return successResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error executing agent for ticket {TicketId}",
                ticketId);

            // Error handling - Log and revert status
            if (ticket != null && agentEntity != null)
            {
                try
                {
                    _logger.LogInformation(
                        "Agent {AgentName} execution failed on ticket {TicketId}. Reverting to To Do status.",
                        agentEntity.Name,
                        ticketId);

                    // Revert status to To Do for retry
                    ticket.UpdateStatus(ToDoStatusId);
                    await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

                    var ticketIdForNotification = BuildCompositeTicketId(ticket);
                    await _notificationService.NotifyTicketStatusChangedAsync(
                        new TicketStatusChangedNotification(
                            ticket.WorkspaceId,
                            ticketIdForNotification,
                            "To Do",
                            "In Progress"),
                        cancellationToken);
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(
                        innerEx,
                        "Failed to revert ticket status for {TicketId}",
                        ticketId);
                }
            }

            var failureResult = AgentExecutionResult.Failure(ex.Message);

            await DispatchNotificationAsync(
                ticket, agentEntity, failureResult, cancellationToken);

            return failureResult;
        }
        finally
        {
            // Always set agent back to Idle
            if (agentEntity != null)
            {
                try
                {
                    agentEntity.UpdateStatus(AgentStatus.Idle);
                    await _agentDataAccess.UpdateAsync(agentEntity, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to set agent {AgentId} back to Idle status",
                        agentEntity.Id);
                }
            }
        }
    }

    private async Task DispatchNotificationAsync(
        Ticket? ticket,
        Agent? agentEntity,
        AgentExecutionResult result,
        CancellationToken cancellationToken)
    {
        if (ticket is null || agentEntity is null)
            return;

        try
        {
            var notification = new AgentExecutionCompletedNotification(
                WorkspaceId: ticket.WorkspaceId,
                AgentId: agentEntity.Id,
                AgentName: agentEntity.Name,
                TicketId: ticket.Id,
                TicketTitle: ticket.Title,
                Status: result.IsSuccess ? "success" : "failed",
                Summary: result.IsSuccess ? result.Message ?? string.Empty : result.ErrorMessage ?? string.Empty,
                ReviewUrl: result.ReviewUrl);

            await _notificationService.NotifyAgentExecutionCompletedAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send execution completion notification for ticket {TicketId}",
                ticket.Id);
        }
    }

    private string BuildCompositeTicketId(Ticket ticket)
    {
        return (ticket.IntegrationId.HasValue && !string.IsNullOrEmpty(ticket.ExternalTicketId))
            ? _ticketIdParsingService.BuildCompositeId(ticket.IntegrationId.Value, ticket.ExternalTicketId)
            : ticket.Id.ToString();
    }
}
