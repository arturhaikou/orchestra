namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when attempting to create an integration with a duplicate name within a workspace.
/// </summary>
public class DuplicateIntegrationException : Exception
{
    public string IntegrationName { get; }
    public Guid WorkspaceId { get; }

    public DuplicateIntegrationException(string integrationName, Guid workspaceId) 
        : base($"An integration with name '{integrationName}' already exists in this workspace.")
    {
        IntegrationName = integrationName;
        WorkspaceId = workspaceId;
    }
}