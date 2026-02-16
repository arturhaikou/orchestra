using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Tools;

public class AgentToolActionDataAccess : IAgentToolActionDataAccess
{
    private readonly AppDbContext _context;

    public AgentToolActionDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Guid>> GetToolActionIdsByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentToolActions
            .AsNoTracking()
            .Where(ata => ata.AgentId == agentId)
            .Select(ata => ata.ToolActionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> GetUniqueCategoryNamesByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentToolActions
            .AsNoTracking()
            .Where(ata => ata.AgentId == agentId)
            .Join(
                _context.ToolActions,
                ata => ata.ToolActionId,
                ta => ta.Id,
                (ata, ta) => ta)
            .Join(
                _context.ToolCategories,
                ta => ta.ToolCategoryId,
                tc => tc.Id,
                (ta, tc) => tc.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task AssignToolActionsAsync(
        Guid agentId,
        List<Guid> toolActionIds,
        CancellationToken cancellationToken = default)
    {
        if (toolActionIds == null || toolActionIds.Count == 0)
            return;

        // Get existing tool action IDs for the agent
        var existingToolActionIds = await _context.AgentToolActions
            .AsNoTracking()
            .Where(ata => ata.AgentId == agentId)
            .Select(ata => ata.ToolActionId)
            .ToListAsync(cancellationToken);

        // Find tool action IDs that are not already assigned
        var newToolActionIds = toolActionIds
            .Except(existingToolActionIds)
            .ToList();

        if (newToolActionIds.Count == 0)
            return;

        // Create new AgentToolAction entities for the new assignments
        var newAssignments = newToolActionIds
            .Select(toolActionId => AgentToolAction.Create(agentId, toolActionId))
            .ToList();

        // Add the new assignments
        await _context.AgentToolActions.AddRangeAsync(newAssignments, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveToolActionsAsync(
        Guid agentId,
        List<Guid> toolActionIds,
        CancellationToken cancellationToken = default)
    {
        if (toolActionIds == null || !toolActionIds.Any())
        {
            return;
        }

        // Find matching assignments to remove
        var assignmentsToRemove = await _context.AgentToolActions
            .Where(ata => ata.AgentId == agentId && toolActionIds.Contains(ata.ToolActionId))
            .ToListAsync(cancellationToken);

        if (!assignmentsToRemove.Any())
        {
            return;
        }

        // Bulk delete
        _context.AgentToolActions.RemoveRange(assignmentsToRemove);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAllToolActionsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        // Find all assignments for the agent
        var assignmentsToRemove = await _context.AgentToolActions
            .Where(ata => ata.AgentId == agentId)
            .ToListAsync(cancellationToken);

        if (!assignmentsToRemove.Any())
        {
            return;
        }

        // Bulk delete
        _context.AgentToolActions.RemoveRange(assignmentsToRemove);
        await _context.SaveChangesAsync(cancellationToken);
    }
}