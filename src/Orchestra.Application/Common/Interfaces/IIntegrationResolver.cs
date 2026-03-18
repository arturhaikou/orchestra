using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Resolves and validates an Integration record for use by external tool services.
/// Enforces all integration validation rules (non-empty ID, provider type match,
/// workspace ownership, active status) in a single authoritative location.
/// </summary>
public interface IIntegrationResolver
{
    /// <summary>
    /// Retrieves the active integration identified by <paramref name="integrationId"/> and
    /// validates that it belongs to <paramref name="workspaceId"/> and matches
    /// <paramref name="providerType"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace that must own the integration.</param>
    /// <param name="integrationId">The raw string integration ID supplied by the LLM tool call. May be empty.</param>
    /// <param name="providerType">The expected provider type (e.g. JIRA, GITHUB).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fully validated <see cref="Integration"/> record.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="integrationId"/> is null/whitespace, the integration is not found,
    /// the integration belongs to a different workspace, or the provider type does not match.
    /// </exception>
    Task<Integration> ResolveAsync(
        Guid workspaceId,
        string integrationId,
        ProviderType providerType,
        CancellationToken cancellationToken = default);
}
