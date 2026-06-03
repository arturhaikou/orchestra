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

    public async Task<Dictionary<Guid, List<string>>> GetSystemToolsByDefinitionIdAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        var stepIds = await _context.WorkflowSteps
            .Where(s => s.WorkflowDefinitionId == workflowDefinitionId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var tools = await _context.WorkflowStepSystemTools
            .Where(t => stepIds.Contains(t.WorkflowStepId))
            .ToListAsync(cancellationToken);

        return tools
            .GroupBy(t => t.WorkflowStepId)
            .ToDictionary(g => g.Key, g => g.Select(t => t.ToolIdentifier).ToList());
    }

    public async Task ReplaceStepSystemToolsAsync(Guid stepId, List<string> toolIdentifiers, CancellationToken cancellationToken = default)
    {
        var existing = await _context.WorkflowStepSystemTools
            .Where(t => t.WorkflowStepId == stepId)
            .ToListAsync(cancellationToken);

        _context.WorkflowStepSystemTools.RemoveRange(existing);

        var newTools = toolIdentifiers
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => WorkflowStepSystemTool.Create(stepId, id))
            .ToList();

        if (newTools.Count > 0)
            await _context.WorkflowStepSystemTools.AddRangeAsync(newTools, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
