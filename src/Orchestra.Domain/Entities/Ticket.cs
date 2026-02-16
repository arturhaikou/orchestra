using System.Collections.Generic;
using System;

namespace Orchestra.Domain.Entities;

public class Ticket
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public Guid? PriorityId { get; private set; }
    public Guid? StatusId { get; private set; }
    public bool IsInternal { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // External ticket reference (null for pure internal tickets)
    public Guid? IntegrationId { get; private set; }
    public string? ExternalTicketId { get; private set; } // e.g., "PROJ-123"

    // Assignment fields (null for unassigned)
    public Guid? AssignedAgentId { get; private set; }
    public Guid? AssignedWorkflowId { get; private set; }

    // Navigation properties
    public Integration? Integration { get; private set; }
    public ICollection<TicketComment> Comments { get; private set; } = new List<TicketComment>();

    private Ticket() { } // EF Core constructor

    public static Ticket Create(
        Guid workspaceId,
        string title,
        string description,
        Guid? priorityId,
        Guid? statusId,
        bool isInternal)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (isInternal)
        {
            if (priorityId == null)
                throw new ArgumentException("Priority ID is required for internal tickets.", nameof(priorityId));
            if (statusId == null)
                throw new ArgumentException("Status ID is required for internal tickets.", nameof(statusId));
        }

        return new Ticket
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Title = title,
            Description = description,
            PriorityId = priorityId,
            StatusId = statusId,
            IsInternal = isInternal,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a materialized external ticket record when assignments are first made to external tickets.
    /// Title and Description are set to empty strings as placeholders; they will be fetched dynamically from the provider.
    /// StatusId and PriorityId track internal agent execution state while external values remain in the provider.
    /// </summary>
    public static Ticket MaterializeFromExternal(
        Guid workspaceId,
        Guid integrationId,
        string externalTicketId,
        Guid? statusId = null,
        Guid? priorityId = null,
        Guid? assignedAgentId = null,
        Guid? assignedWorkflowId = null)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));
        
        if (integrationId == Guid.Empty)
            throw new ArgumentException("Integration ID is required.", nameof(integrationId));
        
        if (string.IsNullOrWhiteSpace(externalTicketId))
            throw new ArgumentException("External ticket ID is required.", nameof(externalTicketId));

        return new Ticket
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            IntegrationId = integrationId,
            ExternalTicketId = externalTicketId,
            IsInternal = false,
            // Title, Description will come from provider dynamically
            Title = string.Empty, // Placeholder, will be overridden from provider
            Description = string.Empty,
            // StatusId and PriorityId track internal state for agent execution
            PriorityId = priorityId,
            StatusId = statusId,
            AssignedAgentId = assignedAgentId,
            AssignedWorkflowId = assignedWorkflowId,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates status for both internal and materialized external tickets.
    /// For external tickets, this tracks internal agent execution state while the provider maintains its own status.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when statusId is empty</exception>
    public void UpdateStatus(Guid statusId)
    {
        if (statusId == Guid.Empty)
            throw new ArgumentException("Status ID is required.", nameof(statusId));
        
        StatusId = statusId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates priority for both internal and materialized external tickets.
    /// For external tickets, this tracks internal priority while the provider maintains its own priority.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when priorityId is empty</exception>
    public void UpdatePriority(Guid priorityId)
    {
        if (priorityId == Guid.Empty)
            throw new ArgumentException("Priority ID is required.", nameof(priorityId));
        
        PriorityId = priorityId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates assignments for both internal and external tickets.
    /// This is the only field that can be modified for external tickets.
    /// Validates that assigned entities belong to the same workspace as the ticket.
    /// </summary>
    /// <param name="assignedAgentId">The ID of the agent to assign (or null to unassign)</param>
    /// <param name="agentWorkspaceId">The workspace ID of the agent (required if assignedAgentId is provided)</param>
    /// <param name="assignedWorkflowId">The ID of the workflow to assign (or null to unassign)</param>
    /// <param name="workflowWorkspaceId">The workspace ID of the workflow (required if assignedWorkflowId is provided)</param>
    /// <exception cref="unifiedaitracker.Domain.Exceptions.InvalidWorkspaceAssignmentException">
    /// Thrown when the agent or workflow workspace does not match the ticket's workspace
    /// </exception>
    public void UpdateAssignments(
        Guid? assignedAgentId, 
        Guid? agentWorkspaceId,
        Guid? assignedWorkflowId, 
        Guid? workflowWorkspaceId)
    {
        // Validate agent workspace consistency
        if (assignedAgentId.HasValue && agentWorkspaceId.HasValue)
        {
            if (agentWorkspaceId.Value != WorkspaceId)
            {
                throw new Orchestra.Domain.Exceptions.InvalidWorkspaceAssignmentException(
                    "Agent", 
                    assignedAgentId.Value, 
                    WorkspaceId, 
                    agentWorkspaceId.Value);
            }
        }
        
        // Validate workflow workspace consistency
        if (assignedWorkflowId.HasValue && workflowWorkspaceId.HasValue)
        {
            if (workflowWorkspaceId.Value != WorkspaceId)
            {
                throw new Orchestra.Domain.Exceptions.InvalidWorkspaceAssignmentException(
                    "Workflow", 
                    assignedWorkflowId.Value, 
                    WorkspaceId, 
                    workflowWorkspaceId.Value);
            }
        }
        
        // Apply assignments after validation passes
        AssignedAgentId = assignedAgentId;
        AssignedWorkflowId = assignedWorkflowId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the description of an internal ticket.
    /// External tickets cannot update their description as it's managed by the provider.
    /// </summary>
    /// <param name="description">The new description text (required, max 5000 characters)</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to update description on external tickets
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when description is empty or exceeds 5000 characters
    /// </exception>
    public void UpdateDescription(string description)
    {
        if (!IsInternal)
        {
            throw new InvalidOperationException(
                "Cannot update description of external tickets. Description is managed by the external provider.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be empty.", nameof(description));
        }

        if (description.Length > 5000)
        {
            throw new ArgumentException("Description cannot exceed 5000 characters.", nameof(description));
        }

        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates if ticket can be deleted (internal only, no external reference).
    /// External tickets cannot be deleted as they are managed by external providers.
    /// </summary>
    /// <returns>True if ticket can be deleted, false otherwise</returns>
    public bool CanDelete()
    {
        return IsInternal && !IntegrationId.HasValue;
    }

    /// <summary>
    /// Converts an internal ticket to an external ticket reference.
    /// This establishes a link to an external tracker system (Jira, Azure DevOps, etc.).
    /// </summary>
    /// <param name="integrationId">The integration ID linking to the external tracker.</param>
    /// <param name="externalTicketId">The external ticket identifier (e.g., "PROJ-123").</param>
    /// <exception cref="InvalidOperationException">Thrown when attempting to convert an already external ticket.</exception>
    /// <exception cref="ArgumentException">Thrown when integrationId or externalTicketId are invalid.</exception>
    public void ConvertToExternal(Guid integrationId, string externalTicketId)
    {
        if (!IsInternal)
            throw new InvalidOperationException("Cannot convert external tickets. Ticket is already external.");
        
        if (integrationId == Guid.Empty)
            throw new ArgumentException("Integration ID is required.", nameof(integrationId));
        
        if (string.IsNullOrWhiteSpace(externalTicketId))
            throw new ArgumentException("External ticket ID is required.", nameof(externalTicketId));
        
        IntegrationId = integrationId;
        ExternalTicketId = externalTicketId;
        IsInternal = false;
        StatusId = null;    // External tickets use provider status
        PriorityId = null;  // External tickets use provider priority
        UpdatedAt = DateTime.UtcNow;
    }
}