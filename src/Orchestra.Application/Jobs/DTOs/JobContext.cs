using Orchestra.Domain.Enums;

namespace Orchestra.Application.Jobs.DTOs;

public record JobContext(
    Guid WorkspaceId,
    Guid AgentId,
    string AgentName,
    JobTriggerType TriggerType,
    string InitialPrompt,
    Guid? TicketId = null,
    string? TicketTitle = null,
    Guid? ParentJobId = null,
    Guid? WorkflowExecutionId = null);
