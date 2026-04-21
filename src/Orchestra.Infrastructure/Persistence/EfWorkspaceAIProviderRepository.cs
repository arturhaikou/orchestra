using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceAIProviderRepository"/>.
/// Stages changes without calling SaveChangesAsync — the caller (Unit of Work) is
/// responsible for committing.
/// </summary>
public sealed class EfWorkspaceAIProviderRepository : IWorkspaceAIProviderRepository
{
    private readonly AppDbContext _context;

    public EfWorkspaceAIProviderRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<AIProviderConfiguration?> GetByWorkspaceIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await _context.AIProviderConfigurations
            .FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task AddAsync(AIProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        _context.AIProviderConfigurations.Add(configuration);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateAsync(AIProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        _context.AIProviderConfigurations.Update(configuration);
        return Task.CompletedTask;
    }
}
