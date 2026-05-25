using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Common.Interfaces;

public interface IJobDataAccess
{
    Task<Guid> CreateAsync(Job job, CancellationToken cancellationToken = default);
    Task UpdateAsync(Job job, CancellationToken cancellationToken = default);
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Job> Items, int Total)> GetPagedByWorkspaceAsync(
        Guid workspaceId,
        JobStatus? statusFilter,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default);

    Task AddStepAsync(JobStep step, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobStep>> GetStepsByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<int> GetMaxSequenceAsync(Guid jobId, CancellationToken cancellationToken = default);
}
