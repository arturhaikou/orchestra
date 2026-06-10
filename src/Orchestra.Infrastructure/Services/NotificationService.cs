using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Workflows.DTOs;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Hubs;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        AppDbContext db,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _db = db;
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

    public async Task NotifyAgentQuestionAskedAsync(
        Guid workspaceId,
        Guid questionId,
        Guid? jobId = null,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{workspaceId}";

        var payload = new
        {
            workspaceId = workspaceId,
            questionId = questionId
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("AgentQuestionAsked", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched AgentQuestionAsked notification. WorkspaceId={WorkspaceId} QuestionId={QuestionId}",
                workspaceId,
                questionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch AgentQuestionAsked notification. WorkspaceId={WorkspaceId} QuestionId={QuestionId}",
                workspaceId,
                questionId);
        }

        await TryDispatchGlobalQuestionNotificationAsync(workspaceId, questionId, jobId, cancellationToken);
    }

    public async Task NotifyAgentQuestionAnsweredAsync(
        Guid workspaceId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workspace = await _db.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
            if (workspace is null)
                return;

            var userGroupName = $"user-{workspace.OwnerId}";
            await _hubContext.Clients.Group(userGroupName)
                .SendAsync("GlobalAgentQuestionResolved", new { questionId }, cancellationToken);

            var workspaceGroupName = $"workspace-{workspaceId}";
            await _hubContext.Clients.Group(workspaceGroupName)
                .SendAsync("AgentQuestionResolved", new { workspaceId, questionId }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to dispatch AgentQuestionAnswered notifications. WorkspaceId={WorkspaceId} QuestionId={QuestionId}",
                workspaceId, questionId);
        }
    }

    private async Task TryDispatchGlobalQuestionNotificationAsync(
        Guid workspaceId,
        Guid questionId,
        Guid? jobId,
        CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await _db.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
            if (workspace is null)
                return;

            var question = await _db.AgentQuestions
                .FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);
            if (question is null)
                return;

            string? ticketTitle = null;
            Guid? ticketId = null;
            string agentName = "Agent";

            if (jobId.HasValue)
            {
                var job = await _db.Jobs
                    .FirstOrDefaultAsync(j => j.Id == jobId.Value, cancellationToken);
                if (job is not null)
                {
                    ticketTitle = job.TicketTitle;
                    ticketId = job.TicketId;
                    agentName = job.AgentName;
                }
            }

            var camelCase = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var questionsJson = JsonSerializer.Serialize(
                JsonSerializer.Deserialize<List<QuestionItem>>(question.QuestionsJson), camelCase);

            var globalPayload = new
            {
                workspaceId,
                workspaceName = workspace.Name,
                questionId,
                ticketId,
                ticketTitle,
                agentName,
                questionsJson,
                createdAt = question.CreatedAt
            };

            var userGroupName = $"user-{workspace.OwnerId}";
            await _hubContext.Clients.Group(userGroupName)
                .SendAsync("GlobalAgentQuestionAsked", globalPayload, cancellationToken);

            _logger.LogDebug(
                "Dispatched GlobalAgentQuestionAsked notification. WorkspaceId={WorkspaceId} QuestionId={QuestionId} OwnerId={OwnerId}",
                workspaceId, questionId, workspace.OwnerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch GlobalAgentQuestionAsked notification. WorkspaceId={WorkspaceId} QuestionId={QuestionId}",
                workspaceId, questionId);
        }
    }

    public async Task NotifyWorkflowStepStartedAsync(
        WorkflowStepStartedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{notification.WorkspaceId}";

        var payload = new
        {
            workflowExecutionId = notification.WorkflowExecutionId,
            ticketId = notification.TicketId,
            stepIndex = notification.StepIndex
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("WorkflowStepStarted", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched WorkflowStepStarted. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId} StepIndex={StepIndex}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId,
                notification.StepIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch WorkflowStepStarted. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId);
        }
    }

    public async Task NotifyWorkflowStepCompletedAsync(
        WorkflowStepCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{notification.WorkspaceId}";

        var payload = new
        {
            workflowExecutionId = notification.WorkflowExecutionId,
            ticketId = notification.TicketId,
            stepIndex = notification.StepIndex,
            status = notification.Status
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("WorkflowStepCompleted", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched WorkflowStepCompleted. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId} StepIndex={StepIndex} Status={Status}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId,
                notification.StepIndex,
                notification.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch WorkflowStepCompleted. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId);
        }
    }

    public async Task NotifyWorkflowExecutionStatusChangedAsync(
        WorkflowExecutionStatusChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{notification.WorkspaceId}";

        var payload = new
        {
            workflowExecutionId = notification.WorkflowExecutionId,
            ticketId = notification.TicketId,
            status = notification.Status
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("WorkflowExecutionStatusChanged", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched WorkflowExecutionStatusChanged. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId} Status={Status}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId,
                notification.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch WorkflowExecutionStatusChanged. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId);
        }
    }

    public async Task NotifyWorkflowStepJobAssignedAsync(
        WorkflowStepJobAssignedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{notification.WorkspaceId}";

        var payload = new
        {
            workflowExecutionId = notification.WorkflowExecutionId,
            ticketId = notification.TicketId,
            stepIndex = notification.StepIndex,
            jobId = notification.JobId
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("WorkflowStepJobAssigned", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched WorkflowStepJobAssigned. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId} StepIndex={StepIndex} JobId={JobId}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId,
                notification.StepIndex,
                notification.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch WorkflowStepJobAssigned. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId);
        }
    }

    public async Task NotifyWorkflowTicketSwitchedAsync(
        WorkflowTicketSwitchedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{notification.WorkspaceId}";

        var payload = new
        {
            workflowExecutionId = notification.WorkflowExecutionId,
            workflowId = notification.WorkflowId,
            previousTicketId = notification.PreviousTicketId,
            newTicketId = notification.NewTicketId,
            externalTicketKey = notification.ExternalTicketKey
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("WorkflowTicketSwitched", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched WorkflowTicketSwitched. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId} ExternalKey={ExternalKey}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId,
                notification.ExternalTicketKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch WorkflowTicketSwitched. WorkspaceId={WorkspaceId} ExecutionId={ExecutionId}",
                notification.WorkspaceId,
                notification.WorkflowExecutionId);
        }
    }
}
