using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Persistence;

public class JobDataAccess : IJobDataAccess
{
    private readonly AppDbContext _context;

    public JobDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> CreateAsync(Job job, CancellationToken cancellationToken = default)
    {
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var trackedEntry = _context.ChangeTracker.Entries<Job>()
            .FirstOrDefault(e => e.Entity.Id == job.Id);

        if (trackedEntry != null)
        {
            trackedEntry.CurrentValues.SetValues(job);
        }
        else
        {
            _context.Jobs.Update(job);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Job> Items, int Total)> GetPagedByWorkspaceAsync(
        Guid workspaceId,
        JobStatus? statusFilter,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Jobs
            .AsNoTracking()
            .Where(j => j.WorkspaceId == workspaceId);

        if (statusFilter.HasValue)
            query = query.Where(j => j.Status == statusFilter.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddStepAsync(JobStep step, CancellationToken cancellationToken = default)
    {
        _context.JobSteps.Add(step);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobStep>> GetStepsByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _context.JobSteps
            .AsNoTracking()
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.Sequence)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetMaxSequenceAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _context.JobSteps
            .Where(s => s.JobId == jobId)
            .MaxAsync(s => (int?)s.Sequence, cancellationToken) ?? 0;
    }

    public async Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .AsNoTracking()
            .Where(j => j.Status == status)
            .ToListAsync(cancellationToken);
    }
}
