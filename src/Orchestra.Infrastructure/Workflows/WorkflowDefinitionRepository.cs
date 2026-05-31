using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Workflows;

public class WorkflowDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly AppDbContext _context;

    public WorkflowDefinitionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<List<WorkflowDefinition>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowDefinitions
            .Where(w => w.WorkspaceId == workspaceId)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowStep>> GetStepsByDefinitionIdAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowSteps
            .Where(s => s.WorkflowDefinitionId == workflowDefinitionId)
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        await _context.WorkflowDefinitions.AddAsync(definition, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _context.WorkflowDefinitions.Update(definition);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await _context.WorkflowDefinitions.FindAsync([id], cancellationToken);
        if (definition is not null)
        {
            _context.WorkflowDefinitions.Remove(definition);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ReplaceStepsAsync(Guid workflowDefinitionId, List<WorkflowStep> steps, CancellationToken cancellationToken = default)
    {
        var existing = await _context.WorkflowSteps
            .Where(s => s.WorkflowDefinitionId == workflowDefinitionId)
            .ToListAsync(cancellationToken);

        _context.WorkflowSteps.RemoveRange(existing);
        await _context.WorkflowSteps.AddRangeAsync(steps, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
