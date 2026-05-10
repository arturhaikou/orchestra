using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Integrations;

/// <summary>
/// Single authoritative implementation of <see cref="IIntegrationResolver"/>.
/// Enforces every integration validation rule in one place:
///   1. integrationId must be non-empty
///   2. integrationId must be a parseable GUID
///   3. The integration must exist and be active (enforced by GetByIdAsync)
///   4. The integration must belong to the requesting workspace
///   5. The integration's provider type must match the expected type
/// </summary>
internal sealed class IntegrationResolver : IIntegrationResolver
{
    private readonly IIntegrationDataAccess _integrationDataAccess;

    public IntegrationResolver(IIntegrationDataAccess integrationDataAccess)
    {
        _integrationDataAccess = integrationDataAccess;
    }

    /// <inheritdoc />
    public async Task<Integration> ResolveAsync(
        Guid workspaceId,
        string integrationId,
        ProviderType providerType,
        CancellationToken cancellationToken = default)
    {
        // Rule 1: integrationId is mandatory (FR-02 Scenario 2)
        if (string.IsNullOrWhiteSpace(integrationId))
        {
            throw new InvalidOperationException(
                "integrationId is required for this tool action; no integration credentials were accessed.");
        }

        // Rule 2: integrationId must be a valid GUID
        if (!Guid.TryParse(integrationId, out var integrationGuid))
        {
            throw new InvalidOperationException(
                "No active integration found for the supplied ID within this workspace.");
        }

        // Rule 3: The integration must exist and be active
        // GetByIdAsync already enforces IsActive = true at the data layer
        var integration = await _integrationDataAccess.GetByIdAsync(integrationGuid, cancellationToken);

        if (integration == null)
        {
            throw new InvalidOperationException(
                "No active integration found for the supplied ID within this workspace.");
        }

        // Rule 4: Workspace ownership — prevent cross-workspace access (FR-02 Scenario 4)
        // Return the same generic not-found message to avoid leaking data about other workspaces
        if (integration.WorkspaceId != workspaceId)
        {
            throw new InvalidOperationException(
                "No active integration found for the supplied ID within this workspace.");
        }

        // Rule 5: Provider type must match the expected type (FR-02 Scenario 3)
        if (integration.Provider != providerType)
        {
            var friendlyName = GetProviderFriendlyName(providerType);
            throw new InvalidOperationException(
                $"No active {friendlyName} integration found for the supplied integrationId; the specified integration is not a {friendlyName} integration.");
        }

        return integration;
    }

    private static string GetProviderFriendlyName(ProviderType providerType) => providerType switch
    {
        ProviderType.JIRA => "Jira",
        ProviderType.GITHUB => "GitHub",
        ProviderType.GITLAB => "GitLab",
        ProviderType.CONFLUENCE => "Confluence",
        _ => providerType.ToString()
    };
}
