using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Agents;

public class AgentSubAgentDataAccess : IAgentSubAgentDataAccess
{
    private readonly AppDbContext _context;

    public AgentSubAgentDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Guid>> GetSubAgentIdsByParentAgentIdAsync(
        Guid parentAgentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentSubAgents
            .AsNoTracking()
            .Where(asa => asa.ParentAgentId == parentAgentId)
            .Select(asa => asa.SubAgentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetParentAgentIdsBySubAgentIdAsync(
        Guid subAgentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentSubAgents
            .AsNoTracking()
            .Where(asa => asa.SubAgentId == subAgentId)
            .Select(asa => asa.ParentAgentId)
            .ToListAsync(cancellationToken);
    }

    public async Task AssignSubAgentsAsync(
        Guid parentAgentId,
        List<Guid> subAgentIds,
        CancellationToken cancellationToken = default)
    {
        if (subAgentIds == null || subAgentIds.Count == 0)
            return;

        var existingSubAgentIds = await _context.AgentSubAgents
            .AsNoTracking()
            .Where(asa => asa.ParentAgentId == parentAgentId)
            .Select(asa => asa.SubAgentId)
            .ToListAsync(cancellationToken);

        var newSubAgentIds = subAgentIds
            .Except(existingSubAgentIds)
            .ToList();

        if (newSubAgentIds.Count == 0)
            return;

        var newAssignments = newSubAgentIds
            .Select(subAgentId => AgentSubAgent.Create(parentAgentId, subAgentId))
            .ToList();

        await _context.AgentSubAgents.AddRangeAsync(newAssignments, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAllSubAgentsAsync(
        Guid parentAgentId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _context.AgentSubAgents
            .Where(asa => asa.ParentAgentId == parentAgentId)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return;

        _context.AgentSubAgents.RemoveRange(assignments);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveBySubAgentIdAsync(
        Guid subAgentId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _context.AgentSubAgents
            .Where(asa => asa.SubAgentId == subAgentId)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return;

        _context.AgentSubAgents.RemoveRange(assignments);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, List<Guid>>> GetSubAgentIdsByWorkspaceIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        // Join AgentSubAgents with Agents to filter by workspace
        var rows = await _context.AgentSubAgents
            .AsNoTracking()
            .Join(
                _context.Agents.Where(a => a.WorkspaceId == workspaceId),
                asa => asa.ParentAgentId,
                a => a.Id,
                (asa, _) => new { asa.ParentAgentId, asa.SubAgentId })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, List<Guid>>();
        foreach (var row in rows)
        {
            if (!result.TryGetValue(row.ParentAgentId, out var list))
            {
                list = new List<Guid>();
                result[row.ParentAgentId] = list;
            }
            list.Add(row.SubAgentId);
        }
        return result;
    }
}
