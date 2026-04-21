using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Stubs;

/// <summary>
/// Phase 1 stub. Throws <see cref="NotImplementedException"/> for every method.
/// Replace with a real EF Core implementation in Phase 2.
/// </summary>
public sealed class StubWorkspaceAIProviderRepository : IWorkspaceAIProviderRepository
{
    /// <inheritdoc/>
    public Task<AIProviderConfiguration?> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task AddAsync(AIProviderConfiguration configuration, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task UpdateAsync(AIProviderConfiguration configuration, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
