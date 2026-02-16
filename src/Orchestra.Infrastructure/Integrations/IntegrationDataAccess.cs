using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Integrations;

public class IntegrationDataAccess : IIntegrationDataAccess
{
    private readonly AppDbContext _context;

    public IntegrationDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Integration>> GetByWorkspaceIdAsync(
        Guid workspaceId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Integrations
            .Where(i => i.WorkspaceId == workspaceId && i.IsActive)
            .OrderBy(i => i.Type)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Integration?> GetByIdAsync(
        Guid integrationId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Integrations
            .FirstOrDefaultAsync(i => i.Id == integrationId && i.IsActive, cancellationToken);
    }

    public async Task<bool> ExistsByNameInWorkspaceAsync(
        string name, 
        Guid workspaceId, 
        Guid? excludeIntegrationId = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Integrations
            .Where(i => i.Name == name && i.WorkspaceId == workspaceId && i.IsActive);

        if (excludeIntegrationId.HasValue)
        {
            query = query.Where(i => i.Id != excludeIntegrationId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Integration integration, CancellationToken cancellationToken = default)
    {
        await _context.Integrations.AddAsync(integration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Integration integration, CancellationToken cancellationToken = default)
    {
        _context.Integrations.Update(integration);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
