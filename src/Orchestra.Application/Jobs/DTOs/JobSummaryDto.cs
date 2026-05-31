using Orchestra.Domain.Enums;

namespace Orchestra.Application.Jobs.DTOs;

public record JobSummaryDto(
    Guid Id,
    Guid WorkspaceId,
    Guid AgentId,
    string AgentName,
    string? TicketTitle,
    Guid? TicketId,
    JobStatus Status,
    JobTriggerType TriggerType,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    Guid? ParentJobId = null,
    Guid? WorkflowExecutionId = null);
