using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Stubs;

/// <summary>
/// Phase 1 stub. Throws <see cref="NotImplementedException"/> for every method.
/// Replace with a real provider lifecycle orchestration service in Phase 2.
/// </summary>
public sealed class StubWorkspaceProviderService : IWorkspaceProviderService
{
    /// <inheritdoc/>
    public Task<Guid> CreateProviderConfigAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string? defaultModelId,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task UpdateProviderConfigAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string? defaultModelId,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetAvailableModelsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<ProviderValidationResult?> ValidateProviderAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task ReconfigureProviderAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string defaultModelId,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
