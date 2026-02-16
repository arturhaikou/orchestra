using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Agents;

public class AgentDataAccess : IAgentDataAccess
{
    private readonly AppDbContext _context;

    public AgentDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Agent?> GetByIdAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Agent>().FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);
    }

    public async Task<List<Agent>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Agent>()
            .Where(a => a.WorkspaceId == workspaceId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if an agent exists by ID without loading the full entity (performance optimized).
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if the agent exists; otherwise, false.</returns>
    public async Task<bool> ExistsAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Agent>()
            .AnyAsync(a => a.Id == agentId, cancellationToken);
    }

    public async Task AddAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        await _context.Set<Agent>().AddAsync(agent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        _context.Set<Agent>().Update(agent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _context.Set<Agent>().FindAsync(agentId, cancellationToken);
        if (agent != null)
        {
            _context.Set<Agent>().Remove(agent);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}