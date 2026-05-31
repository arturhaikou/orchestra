using Orchestra.Application.Jobs.DTOs;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Jobs.Services;

public interface IJobService
{
    Task<Guid> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken = default);

    Task UpdateJobStatusAsync(
        Guid jobId,
        JobStatus status,
        string? finalResponse = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task<PagedJobsResult> GetJobsAsync(
        Guid workspaceId,
        GetJobsQuery query,
        CancellationToken cancellationToken = default);

    Task<JobDetailDto?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions the job to WaitingForInput and records which question caused the suspension.
    /// </summary>
    Task SuspendJobAsync(
        Guid jobId,
        Guid questionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a parent workflow job that tracks the overall state of a workflow execution.
    /// </summary>
    Task<Guid> CreateWorkflowJobAsync(
        Guid workspaceId,
        string workflowName,
        Guid workflowExecutionId,
        Guid? ticketId,
        string? ticketTitle,
        CancellationToken cancellationToken = default);
}
