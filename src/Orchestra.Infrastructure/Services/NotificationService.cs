using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Hubs;

namespace Orchestra.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyAgentExecutionCompletedAsync(
        AgentExecutionCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{notification.WorkspaceId}";

        var payload = new
        {
            agentId = notification.AgentId,
            agentName = notification.AgentName,
            ticketId = notification.TicketId,
            ticketTitle = notification.TicketTitle,
            status = notification.Status,
            summary = notification.Summary,
            reviewUrl = notification.ReviewUrl
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("AgentExecutionCompleted", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched AgentExecutionCompleted notification. WorkspaceId={WorkspaceId} AgentId={AgentId} TicketId={TicketId} Status={Status}",
                notification.WorkspaceId,
                notification.AgentId,
                notification.TicketId,
                notification.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch AgentExecutionCompleted notification. WorkspaceId={WorkspaceId} AgentId={AgentId} TicketId={TicketId}",
                notification.WorkspaceId,
                notification.AgentId,
                notification.TicketId);
        }
    }

    public async Task NotifyTicketStatusChangedAsync(
        TicketStatusChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var groupName = $"workspace-{notification.WorkspaceId}";

        var payload = new
        {
            workspaceId = notification.WorkspaceId,
            ticketId = notification.TicketId,
            newStatus = notification.NewStatus,
            previousStatus = notification.PreviousStatus
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("TicketStatusChanged", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched TicketStatusChanged notification. WorkspaceId={WorkspaceId} TicketId={TicketId} NewStatus={NewStatus}",
                notification.WorkspaceId,
                notification.TicketId,
                notification.NewStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch TicketStatusChanged notification. WorkspaceId={WorkspaceId} TicketId={TicketId}",
                notification.WorkspaceId,
                notification.TicketId);
        }
    }

    public async Task NotifyJobCreatedAsync(Guid workspaceId, JobSummaryDto job, CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{workspaceId}";

        var payload = new
        {
            jobId = job.Id,
            agentId = job.AgentId,
            agentName = job.AgentName,
            status = job.Status,
            triggerType = job.TriggerType,
            ticketId = job.TicketId,
            ticketTitle = job.TicketTitle,
            createdAt = job.CreatedAt
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("JobCreated", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched JobCreated notification. WorkspaceId={WorkspaceId} JobId={JobId} AgentId={AgentId}",
                workspaceId,
                job.Id,
                job.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch JobCreated notification. WorkspaceId={WorkspaceId} JobId={JobId}",
                workspaceId,
                job.Id);
        }
    }

    public async Task NotifyJobStatusChangedAsync(
        Guid workspaceId,
        Guid jobId,
        JobStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{workspaceId}";

        var payload = new
        {
            jobId = jobId,
            newStatus = newStatus
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("JobStatusChanged", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched JobStatusChanged notification. WorkspaceId={WorkspaceId} JobId={JobId} NewStatus={NewStatus}",
                workspaceId,
                jobId,
                newStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch JobStatusChanged notification. WorkspaceId={WorkspaceId} JobId={JobId}",
                workspaceId,
                jobId);
        }
    }

    public async Task NotifyJobStepAddedAsync(
        Guid workspaceId,
        Guid jobId,
        JobStepDto step,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{workspaceId}";

        var payload = new
        {
            jobId = jobId,
            stepId = step.Id,
            stepType = step.StepType,
            sequence = step.Sequence,
            timestamp = step.Timestamp,
            content = step.Content,
            toolName = step.ToolName,
            isJson = step.IsJson,
            durationMs = step.DurationMs,
            isError = step.IsError,
            parentStepId = step.ParentStepId,
            agentId = step.AgentId,
            agentName = step.AgentName
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("JobStepAdded", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched JobStepAdded notification. WorkspaceId={WorkspaceId} JobId={JobId} StepType={StepType}",
                workspaceId,
                jobId,
                step.StepType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch JobStepAdded notification. WorkspaceId={WorkspaceId} JobId={JobId}",
                workspaceId,
                jobId);
        }
    }
}
