using Microsoft.EntityFrameworkCore;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.AiCliIntegrations;

public class AiCliIntegrationDataAccess : IAiCliIntegrationDataAccess
{
    private readonly AppDbContext _context;

    public AiCliIntegrationDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AiCliIntegration>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
        => await _context.AiCliIntegrations
            .Where(a => a.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);

    public async Task<AiCliIntegration?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await _context.AiCliIntegrations
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<bool> ExistsByNameAsync(
        Guid workspaceId,
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AiCliIntegrations
            .Where(a => a.WorkspaceId == workspaceId && a.Name == name);

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(
        AiCliIntegration integration,
        CancellationToken cancellationToken = default)
    {
        await _context.AiCliIntegrations.AddAsync(integration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        AiCliIntegration integration,
        CancellationToken cancellationToken = default)
    {
        _context.AiCliIntegrations.Update(integration);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        AiCliIntegration integration,
        CancellationToken cancellationToken = default)
    {
        _context.AiCliIntegrations.Remove(integration);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
