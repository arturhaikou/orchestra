namespace Orchestra.Domain.Entities;

public class UserWorkspace
{
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    public User? User { get; private set; }
    public Workspace? Workspace { get; private set; }

    private UserWorkspace() { } // For EF Core

    public static UserWorkspace Create(Guid userId, Guid workspaceId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("WorkspaceId cannot be empty.", nameof(workspaceId));
        }

        return new UserWorkspace
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            JoinedAt = DateTime.UtcNow
        };
    }
}