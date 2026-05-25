using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Jobs;

public class JobService : IJobService
{
    private readonly IJobDataAccess _jobDataAccess;
    private readonly INotificationService _notificationService;

    public JobService(IJobDataAccess jobDataAccess, INotificationService notificationService)
    {
        _jobDataAccess = jobDataAccess;
        _notificationService = notificationService;
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
                // Status is set via reflection since there's no MarkSuspended method
                var statusProperty = typeof(Job).GetProperty(nameof(Job.Status),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                statusProperty?.GetSetMethod(nonPublic: true)?.Invoke(job, new object[] { status });
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
            job.CompletedAt);

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
            steps.Select(MapToStepDto).ToList());

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
}
