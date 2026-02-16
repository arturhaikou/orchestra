namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a user attempts to access a workspace-scoped entity
/// but is not a member of the workspace.
/// </summary>
public class WorkspaceAccessDeniedException : Exception
{
    public Guid UserId { get; }
    public Guid WorkspaceId { get; }

    public WorkspaceAccessDeniedException(Guid userId, Guid workspaceId)
        : base($"User {userId} is not a member of workspace {workspaceId}.")
    {
        UserId = userId;
        WorkspaceId = workspaceId;
    }

    public WorkspaceAccessDeniedException(Guid userId, Guid workspaceId, string message)
        : base(message)
    {
        UserId = userId;
        WorkspaceId = workspaceId;
    }

    public WorkspaceAccessDeniedException(Guid userId, Guid workspaceId, string message, Exception innerException)
        : base(message, innerException)
    {
        UserId = userId;
        WorkspaceId = workspaceId;
    }
}