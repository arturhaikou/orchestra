using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Data-access abstraction for reading and persisting <see cref="AIProviderConfiguration"/> records.
/// Each workspace may have at most one <see cref="AIProviderConfiguration"/>.
/// Concrete implementation lives in the Infrastructure layer.
/// </summary>
public interface IWorkspaceAIProviderRepository
{
    /// <summary>
    /// Returns the <see cref="AIProviderConfiguration"/> for the given workspace,
    /// or <see langword="null"/> if the workspace has no provider configured.
    /// </summary>
    /// <param name="workspaceId">The workspace to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AIProviderConfiguration?> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a new <see cref="AIProviderConfiguration"/> record.
    /// </summary>
    /// <param name="configuration">The fully-populated configuration entity to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(AIProviderConfiguration configuration, CancellationToken cancellationToken);

    /// <summary>
    /// Applies changes to an existing <see cref="AIProviderConfiguration"/> record.
    /// </summary>
    /// <param name="configuration">The modified configuration entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(AIProviderConfiguration configuration, CancellationToken cancellationToken);
}
