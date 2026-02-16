using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Tools;

/// <summary>
/// Implementation of tool category data access operations using Entity Framework Core.
/// </summary>
public class ToolCategoryDataAccess : IToolCategoryDataAccess
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCategoryDataAccess"/> class.
    /// </summary>
    /// <param name="context">The application database context.</param>
    public ToolCategoryDataAccess(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<List<ToolCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ToolCategories
            .AsNoTracking()
            .OrderBy(tc => tc.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ToolCategory>> GetByProviderTypesAsync(
        List<ProviderType> providerTypes,
        CancellationToken cancellationToken = default)
    {
        return await _context.ToolCategories
            .AsNoTracking()
            .Where(tc => providerTypes.Contains(tc.ProviderType))
            .OrderBy(tc => tc.ProviderType)
            .ThenBy(tc => tc.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ToolCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ToolCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(tc => tc.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ToolCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.ToolCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(tc => tc.Name == name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(ToolCategory toolCategory, CancellationToken cancellationToken = default)
    {
        await _context.ToolCategories.AddAsync(toolCategory, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ToolCategory toolCategory, CancellationToken cancellationToken = default)
    {
        _context.ToolCategories.Update(toolCategory);
        await _context.SaveChangesAsync(cancellationToken);
    }
}