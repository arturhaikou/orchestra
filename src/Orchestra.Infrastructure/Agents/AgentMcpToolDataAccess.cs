using Microsoft.EntityFrameworkCore;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Agents;

internal sealed class AgentMcpToolDataAccess : IAgentMcpToolDataAccess
{
    private readonly AppDbContext _db;

    public AgentMcpToolDataAccess(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CountDistinctAgentsByServerIdAsync(
        Guid mcpServerId,
        CancellationToken cancellationToken = default)
    {
        return await _db.AgentMcpTools
            .Where(x => x.McpServerId == mcpServerId)
            .Select(x => x.AgentId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<List<AgentMcpTool>> GetByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await _db.AgentMcpTools
            .Where(x => x.AgentId == agentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AgentMcpTool>> GetByAgentAndServerIdAsync(
        Guid agentId,
        Guid mcpServerId,
        CancellationToken cancellationToken = default)
    {
        return await _db.AgentMcpTools
            .Where(x => x.AgentId == agentId && x.McpServerId == mcpServerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<string[]> GetMcpServerNamesByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await _db.AgentMcpTools
            .Where(x => x.AgentId == agentId)
            .Join(
                _db.McpServers,
                amt => amt.McpServerId,
                server => server.Id,
                (amt, server) => server.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(cancellationToken);
    }

    public async Task ReplaceForAgentAndServerAsync(
        Guid agentId,
        Guid mcpServerId,
        IReadOnlyList<AgentMcpTool> replacements,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.AgentMcpTools
            .Where(x => x.AgentId == agentId && x.McpServerId == mcpServerId)
            .ToListAsync(cancellationToken);

        _db.AgentMcpTools.RemoveRange(existing);

        if (replacements.Count > 0)
            await _db.AgentMcpTools.AddRangeAsync(replacements, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllForAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.AgentMcpTools
            .Where(x => x.AgentId == agentId)
            .ToListAsync(cancellationToken);

        _db.AgentMcpTools.RemoveRange(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
