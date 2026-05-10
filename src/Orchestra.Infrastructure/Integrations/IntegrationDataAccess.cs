using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
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
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Integration>> GetMcpServersByWorkspaceIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var integrations = await _context.Integrations
            .Where(i => i.WorkspaceId == workspaceId && i.IsActive)
            .ToListAsync(cancellationToken);

        return integrations
            .Where(i => i.Types.Any(t => t == IntegrationType.MCP_SERVER))
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
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

    public async Task<bool> ExistsByProviderInWorkspaceAsync(
        ProviderType provider,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Integrations
            .Where(i => i.Provider == provider && i.WorkspaceId == workspaceId && i.IsActive)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByProviderInWorkspaceExcludingSelf(
        ProviderType provider,
        Guid workspaceId,
        Guid excludeIntegrationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Integrations
            .Where(i => i.Provider == provider
                     && i.WorkspaceId == workspaceId
                     && i.IsActive
                     && i.Id != excludeIntegrationId)
            .AnyAsync(cancellationToken);
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

    public async Task<List<IntegrationSummaryDto>> GetActiveByWorkspaceAndProvidersAsync(
        Guid workspaceId,
        IEnumerable<ProviderType> providerTypes,
        CancellationToken cancellationToken = default)
    {
        // Materialise to a list once so EF Core can translate the Contains call.
        var providers = providerTypes?.ToList() ?? [];

        // Short-circuit: no external tools assigned → no integrations needed.
        if (providers.Count == 0)
            return [];

        // Credential-safe projection: only Id, Name, Provider are selected.
        // EncryptedApiKey and Username are intentionally excluded at query level.
        return await _context.Integrations
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId
                     && i.IsActive
                     && providers.Contains(i.Provider))
            .OrderBy(i => i.Provider)
            .ThenBy(i => i.Name)
            .Select(i => new IntegrationSummaryDto(i.Id, i.Name, i.Provider))
            .ToListAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(Guid integrationId, CancellationToken cancellationToken = default)
    {
        var integration = await _context.Integrations
            .FirstOrDefaultAsync(i => i.Id == integrationId && i.IsActive, cancellationToken);

        if (integration is null)
            return;

        integration.Deactivate();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
