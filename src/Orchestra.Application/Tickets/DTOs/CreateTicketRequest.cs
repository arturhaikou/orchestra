namespace Orchestra.Application.Tickets.DTOs;

public record CreateTicketRequest(
    Guid WorkspaceId,
    string Title,
    string Description,
    Guid StatusId,
    Guid PriorityId,
    bool Internal,
    Guid? AssignedAgentId = null,
    Guid? AssignedWorkflowId = null);