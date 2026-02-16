using System;

namespace Orchestra.Application.Common.Exceptions
{
    public class UnauthorizedWorkspaceAccessException : Exception
    {
        public Guid? UserId { get; }
        public Guid? WorkspaceId { get; }

        public UnauthorizedWorkspaceAccessException(Guid userId, Guid workspaceId)
            : base($"User '{userId}' is not authorized to access workspace '{workspaceId}'.")
        {
            UserId = userId;
            WorkspaceId = workspaceId;
        }

        public UnauthorizedWorkspaceAccessException(
            Guid userId, 
            Guid workspaceId, 
            Exception innerException)
            : base(
                $"User '{userId}' is not authorized to access workspace '{workspaceId}'.", 
                innerException)
        {
            UserId = userId;
            WorkspaceId = workspaceId;
        }

        public UnauthorizedWorkspaceAccessException(string message, Guid userId, Guid workspaceId)
            : base(message)
        {
            UserId = userId;
            WorkspaceId = workspaceId;
        }
    }
}