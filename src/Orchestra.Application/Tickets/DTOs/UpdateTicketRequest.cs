using System.ComponentModel.DataAnnotations;

namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Request to update ticket assignments and metadata.
/// External tickets can only update assignments (agent/workflow).
/// Internal tickets can update status, priority, assignments, and description.
/// All fields are nullable for partial updates.
/// </summary>
public record UpdateTicketRequest(
    Guid? StatusId,           // Only for internal tickets
    Guid? PriorityId,         // Only for internal tickets
    Guid? AssignedAgentId,    // For both internal and external
    Guid? AssignedWorkflowId, // For both internal and external
    [MaxLength(5000)] string? Description  // Only for internal tickets
);