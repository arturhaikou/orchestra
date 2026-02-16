using System;
using System.Collections.Generic;

namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when attempting to assign tools to an agent that are not
/// appropriate for the workspace's connected integrations.
/// </summary>
public class InvalidToolAssignmentException : Exception
{
    public Guid WorkspaceId { get; }
    public IReadOnlyList<string> InvalidToolNames { get; }

    public InvalidToolAssignmentException(Guid workspaceId, IEnumerable<string> invalidToolNames)
        : base($"Cannot assign tools '{string.Join(", ", invalidToolNames)}' to workspace {workspaceId}. " +
               $"These tools require integrations that are not connected to this workspace.")
    {
        WorkspaceId = workspaceId;
        InvalidToolNames = new List<string>(invalidToolNames);
    }

    public InvalidToolAssignmentException(Guid workspaceId, string message)
        : base(message)
    {
        WorkspaceId = workspaceId;
        InvalidToolNames = new List<string>();
    }
}
