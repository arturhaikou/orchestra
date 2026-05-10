using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Tools;

/// <summary>
/// Provides data access operations for ToolAction entities using explicit joins
/// without relying on navigation properties.
/// </summary>
public class ToolActionDataAccess : IToolActionDataAccess
{
    private readonly AppDbContext _context;

    public ToolActionDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ToolAction>> GetByCategoryIdsAsync(
        List<Guid> categoryIds,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .AsNoTracking()
            .Where(ta => categoryIds.Contains(ta.ToolCategoryId) && ta.IsActive)
            .OrderBy(ta => ta.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ToolAction>> GetByCategoryIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .AsNoTracking()
            .Where(ta => ta.ToolCategoryId == categoryId && ta.IsActive)
            .OrderBy(ta => ta.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ToolAction?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .AsNoTracking()
            .FirstOrDefaultAsync(ta => ta.Id == id, cancellationToken);
    }

    public async Task<List<ToolAction>> GetEnabledByIdsAsync(
        List<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .AsNoTracking()
            .Where(ta => ids.Contains(ta.Id) && ta.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ToolAction toolAction,
        CancellationToken cancellationToken = default)
    {
        await _context.Set<ToolAction>().AddAsync(toolAction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        ToolAction toolAction,
        CancellationToken cancellationToken = default)
    {
        _context.Set<ToolAction>().Update(toolAction);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ToolAction>> GetByMethodNamesAsync(
        List<string> methodNames,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .AsNoTracking()
            .Where(ta => methodNames.Contains(ta.MethodName))
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<ToolAction> toolActions,
        CancellationToken cancellationToken = default)
    {
        await _context.Set<ToolAction>().AddRangeAsync(toolActions, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ToolAction>> GetByNamesAsync(
        List<string> names,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .AsNoTracking()
            .Where(ta => names.Contains(ta.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<ToolAction?> FindByToolCategoryIdAndMethodNameAsync(
        Guid toolCategoryId,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ta => ta.ToolCategoryId == toolCategoryId && ta.MethodName == methodName,
                cancellationToken);
    }

    public async Task<List<ToolAction>> GetByIntegrationIdAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .Where(ta => ta.IntegrationId == integrationId)
            .OrderBy(ta => ta.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ToolAction>> GetActiveByIntegrationIdAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .Where(ta => ta.IntegrationId == integrationId && ta.IsActive)
            .OrderBy(ta => ta.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, int>> CountActiveByIntegrationIdsAsync(
        List<Guid> integrationIds,
        CancellationToken cancellationToken = default)
    {
        var counts = await _context.Set<ToolAction>()
            .AsNoTracking()
            .Where(ta => ta.IntegrationId.HasValue
                && integrationIds.Contains(ta.IntegrationId.Value)
                && ta.IsActive)
            .GroupBy(ta => ta.IntegrationId!.Value)
            .Select(g => new { IntegrationId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.IntegrationId, x => x.Count);
    }

    public async Task UpdateRangeAsync(
        List<ToolAction> toolActions,
        CancellationToken cancellationToken = default)
    {
        if (toolActions.Count == 0)
            return;

        _context.Set<ToolAction>().UpdateRange(toolActions);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> DeactivateByIntegrationIdAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        var toolActions = await _context.Set<ToolAction>()
            .Where(ta => ta.IntegrationId == integrationId && ta.IsActive)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var toolAction in toolActions)
            toolAction.Deactivate(now);

        await _context.SaveChangesAsync(cancellationToken);

        return toolActions.Select(ta => ta.Id).ToList();
    }
}