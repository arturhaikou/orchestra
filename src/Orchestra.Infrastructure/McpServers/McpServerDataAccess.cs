using Microsoft.EntityFrameworkCore;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.McpServers;

public class McpServerDataAccess : IMcpServerDataAccess
{
    private readonly AppDbContext _context;

    public McpServerDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsByNameAsync(
        Guid workspaceId,
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.McpServers
            .Where(s => s.WorkspaceId == workspaceId && s.Name == name);

        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<McpServer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await _context.McpServers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<List<McpServer>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
        => await _context.McpServers
            .Where(s => s.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        await _context.McpServers.AddAsync(server, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        _context.McpServers.Update(server);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        _context.McpServers.Remove(server);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
