namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when attempting to create an integration for a provider
/// that already has an active (non-deleted) integration in the same workspace.
/// </summary>
public class DuplicateProviderIntegrationException : Exception
{
    public string ProviderName { get; }
    public Guid WorkspaceId { get; }

    public DuplicateProviderIntegrationException(string providerName, Guid workspaceId)
        : base($"An active integration for {providerName} already exists in this workspace.")
    {
        ProviderName = providerName;
        WorkspaceId = workspaceId;
    }
}
