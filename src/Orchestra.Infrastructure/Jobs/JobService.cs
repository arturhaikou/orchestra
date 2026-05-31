using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Jobs;

public class JobService : IJobService
{
    private readonly IJobDataAccess _jobDataAccess;
    private readonly INotificationService _notificationService;
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly ITicketIdParsingService _ticketIdParsingService;

    // Status GUIDs from seeding
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CompletedStatusId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    public JobService(
        IJobDataAccess jobDataAccess,
        INotificationService notificationService,
        ITicketDataAccess ticketDataAccess,
        ITicketIdParsingService ticketIdParsingService)
    {
        _jobDataAccess = jobDataAccess;
        _notificationService = notificationService;
        _ticketDataAccess = ticketDataAccess;
        _ticketIdParsingService = ticketIdParsingService;
    }

    public async Task<Guid> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        var job = Job.Create(
            request.WorkspaceId,
            request.AgentId,
            request.AgentName,
            request.TriggerType,
            request.InitialPrompt,
            request.TicketId,
            request.TicketTitle);

        if (request.ParentJobId.HasValue)
            job.SetParent(request.ParentJobId.Value);

        if (request.WorkflowExecutionId.HasValue)
            job.AssignWorkflowExecution(request.WorkflowExecutionId.Value);

        var jobId = await _jobDataAccess.CreateAsync(job, cancellationToken);
        var summary = MapToSummaryDto(job);
        await _notificationService.NotifyJobCreatedAsync(request.WorkspaceId, summary, cancellationToken);

        return jobId;
    }

    public async Task UpdateJobStatusAsync(
        Guid jobId,
        JobStatus status,
        string? finalResponse = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var job = await _jobDataAccess.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
            return;

        ApplyStatusTransition(job, status, finalResponse, errorMessage);
        await _jobDataAccess.UpdateAsync(job, cancellationToken);
        await _notificationService.NotifyJobStatusChangedAsync(job.WorkspaceId, jobId, status, cancellationToken);

        // Defensively update associated ticket status if TicketId is set.
        // Workflow step jobs are excluded — the workflow engine owns the ticket lifecycle.
        if (job.TicketId.HasValue)
        {
            await UpdateAssociatedTicketAsync(job, status, cancellationToken);
        }
    }

    private async Task UpdateAssociatedTicketAsync(
        Job job,
        JobStatus jobStatus,
        CancellationToken cancellationToken)
    {
        if (job.WorkflowExecutionId.HasValue)
            return;

        var ticketId = job.TicketId!.Value;
        try
        {
            var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
            if (ticket is null)
                return;

            switch (jobStatus)
            {
                case JobStatus.Completed:
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
                    break;

                case JobStatus.Failed:
                    ticket.UpdateStatus(ToDoStatusId);
                    await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);
                    var retryTicketIdForNotification = BuildCompositeTicketId(ticket);
                    await _notificationService.NotifyTicketStatusChangedAsync(
                        new TicketStatusChangedNotification(
                            ticket.WorkspaceId,
                            retryTicketIdForNotification,
                            "To Do",
                            "In Progress"),
                        cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - ticket update failures shouldn't crash job status updates
            System.Diagnostics.Debug.WriteLine($"Failed to update ticket {ticketId} for job status {jobStatus}: {ex.Message}");
        }
    }

    public async Task<PagedJobsResult> GetJobsAsync(
        Guid workspaceId,
        GetJobsQuery query,
        CancellationToken cancellationToken = default)
    {
        var offset = (query.Page - 1) * query.PageSize;
        var (items, total) = await _jobDataAccess.GetPagedByWorkspaceAsync(
            workspaceId,
            query.Status,
            offset,
            query.PageSize,
            cancellationToken);

        var dtos = items.Select(MapToSummaryDto).ToList();
        return new PagedJobsResult(dtos, total, query.Page, query.PageSize);
    }

    public async Task<JobDetailDto?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobDataAccess.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
            return null;

        var steps = await _jobDataAccess.GetStepsByJobIdAsync(jobId, cancellationToken);
        return MapToDetailDto(job, steps);
    }

    private static void ApplyStatusTransition(
        Job job,
        JobStatus status,
        string? finalResponse,
        string? errorMessage)
    {
        switch (status)
        {
            case JobStatus.Running:
                job.MarkRunning();
                break;
            case JobStatus.Completed:
                job.MarkCompleted(finalResponse);
                break;
            case JobStatus.Failed:
                job.MarkFailed(errorMessage ?? "Unknown error");
                break;
            case JobStatus.WaitingForInput:
                job.MarkWaitingForInput();
                break;
        }
    }

    private static JobSummaryDto MapToSummaryDto(Job job) =>
        new(
            job.Id,
            job.WorkspaceId,
            job.AgentId,
            job.AgentName,
            job.TicketTitle,
            job.TicketId,
            job.Status,
            job.TriggerType,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.ParentJobId,
            job.WorkflowExecutionId);

    private static JobDetailDto MapToDetailDto(Job job, IReadOnlyList<JobStep> steps) =>
        new(
            job.Id,
            job.WorkspaceId,
            job.AgentId,
            job.AgentName,
            job.TicketTitle,
            job.TicketId,
            job.Status,
            job.TriggerType,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.InitialPrompt,
            job.FinalResponse,
            job.ErrorMessage,
            steps.Select(MapToStepDto).ToList(),
            job.ParentJobId,
            job.WorkflowExecutionId);

    private static JobStepDto MapToStepDto(JobStep step) =>
        new(
            step.Id,
            step.StepType,
            step.Sequence,
            step.Timestamp,
            step.Content,
            step.ToolName,
            step.IsJson,
            step.DurationMs,
            step.IsError,
            step.ParentStepId,
            step.AgentId,
            step.AgentName);

    public async Task SuspendJobAsync(
        Guid jobId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        await UpdateJobStatusAsync(jobId, JobStatus.WaitingForInput, cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateWorkflowJobAsync(
        Guid workspaceId,
        string workflowName,
        Guid workflowExecutionId,
        Guid? ticketId,
        string? ticketTitle,
        CancellationToken cancellationToken = default)
    {
        var job = Job.CreateWorkflowJob(
            workspaceId,
            workflowName,
            workflowExecutionId,
            ticketId,
            ticketTitle);

        var jobId = await _jobDataAccess.CreateAsync(job, cancellationToken);
        var summary = MapToSummaryDto(job);
        await _notificationService.NotifyJobCreatedAsync(workspaceId, summary, cancellationToken);

        return jobId;
    }

    private string BuildCompositeTicketId(Ticket ticket)
    {
        return (ticket.IntegrationId.HasValue && !string.IsNullOrEmpty(ticket.ExternalTicketId))
            ? _ticketIdParsingService.BuildCompositeId(ticket.IntegrationId.Value, ticket.ExternalTicketId)
            : ticket.Id.ToString();
    }
}
