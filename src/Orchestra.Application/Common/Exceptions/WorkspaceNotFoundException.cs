using System;

namespace Orchestra.Application.Common.Exceptions
{
    public class WorkspaceNotFoundException : Exception
    {
        public Guid WorkspaceId { get; }

        public WorkspaceNotFoundException(Guid workspaceId)
            : base($"Workspace with ID '{workspaceId}' was not found.")
        {
            WorkspaceId = workspaceId;
        }

        public WorkspaceNotFoundException(Guid workspaceId, Exception innerException)
            : base($"Workspace with ID '{workspaceId}' was not found.", innerException)
        {
            WorkspaceId = workspaceId;
        }
    }
}