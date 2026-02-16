using System;

namespace Orchestra.Domain.Exceptions;

public class InvalidWorkspaceAssignmentException : ArgumentException
{
    public InvalidWorkspaceAssignmentException(string message) 
        : base(message)
    {
    }
    
    public InvalidWorkspaceAssignmentException(string entityType, Guid entityId, Guid ticketWorkspaceId, Guid entityWorkspaceId)
        : base($"{entityType} {entityId} belongs to workspace {entityWorkspaceId}, but ticket belongs to workspace {ticketWorkspaceId}.")
    {
    }
}