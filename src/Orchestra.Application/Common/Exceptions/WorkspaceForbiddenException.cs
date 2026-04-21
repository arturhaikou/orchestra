namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated workspace member attempts an operation that requires
/// workspace ownership (e.g., provider reconfiguration). Maps to <c>403 Forbidden</c>.
/// </summary>
public sealed class WorkspaceForbiddenException : Exception
{
    public Guid UserId { get; }
    public Guid WorkspaceId { get; }

    public WorkspaceForbiddenException(Guid userId, Guid workspaceId)
        : base($"User '{userId}' does not have owner access to workspace '{workspaceId}'.")
    {
        UserId = userId;
        WorkspaceId = workspaceId;
    }
}
