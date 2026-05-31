using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Workflows.DTOs;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Common.Interfaces;

public interface INotificationService
{
    Task NotifyAgentExecutionCompletedAsync(
        AgentExecutionCompletedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyTicketStatusChangedAsync(
        TicketStatusChangedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyJobCreatedAsync(Guid workspaceId, JobSummaryDto job, CancellationToken cancellationToken = default);
    Task NotifyJobStatusChangedAsync(Guid workspaceId, Guid jobId, JobStatus newStatus, CancellationToken cancellationToken = default);
    Task NotifyJobStepAddedAsync(Guid workspaceId, Guid jobId, JobStepDto step, CancellationToken cancellationToken = default);
    Task NotifyAgentQuestionAskedAsync(
        Guid workspaceId,
        Guid questionId,
        CancellationToken cancellationToken = default);

    Task NotifyWorkflowStepStartedAsync(WorkflowStepStartedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyWorkflowStepCompletedAsync(WorkflowStepCompletedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyWorkflowExecutionStatusChangedAsync(WorkflowExecutionStatusChangedNotification notification, CancellationToken cancellationToken = default);
}
