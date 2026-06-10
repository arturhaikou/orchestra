using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Workflows;

public class WorkflowExecutionRepository : IWorkflowExecutionRepository
{
    private readonly AppDbContext _context;

    public WorkflowExecutionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<WorkflowExecution?> GetActiveByTicketIdAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowExecutions
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId
                     && e.Status != WorkflowExecutionStatus.Completed
                     && e.Status != WorkflowExecutionStatus.Failed)
            .OrderByDescending(e => e.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<WorkflowExecution>> GetByTicketIdAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowExecutions
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId)
            .OrderByDescending(e => e.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WorkflowExecution execution, CancellationToken cancellationToken = default)
    {
        await _context.WorkflowExecutions.AddAsync(execution, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowExecution execution, CancellationToken cancellationToken = default)
    {
        _context.WorkflowExecutions.Update(execution);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<WorkflowStepExecution>> GetStepExecutionsByExecutionIdAsync(Guid workflowExecutionId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowStepExecutions
            .Where(s => s.WorkflowExecutionId == workflowExecutionId)
            .OrderBy(s => s.StepIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowStepExecution?> GetStepExecutionByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowStepExecutions
            .FirstOrDefaultAsync(s => s.JobId == jobId, cancellationToken);
    }

    public async Task AddStepExecutionAsync(WorkflowStepExecution stepExecution, CancellationToken cancellationToken = default)
    {
        await _context.WorkflowStepExecutions.AddAsync(stepExecution, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStepExecutionAsync(WorkflowStepExecution stepExecution, CancellationToken cancellationToken = default)
    {
        _context.WorkflowStepExecutions.Update(stepExecution);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
