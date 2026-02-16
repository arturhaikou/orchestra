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
            .Where(ta => categoryIds.Contains(ta.ToolCategoryId))
            .OrderBy(ta => ta.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ToolAction>> GetByCategoryIdAsync(
        Guid categoryId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ToolAction>()
            .AsNoTracking()
            .Where(ta => ta.ToolCategoryId == categoryId)
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
}