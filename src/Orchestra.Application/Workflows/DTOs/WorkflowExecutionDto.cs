using Orchestra.Domain.Enums;

namespace Orchestra.Application.Workflows.DTOs;

public record WorkflowExecutionDto(
    Guid Id,
    Guid WorkflowDefinitionId,
    Guid TicketId,
    Guid WorkspaceId,
    WorkflowExecutionStatus Status,
    int CurrentStepIndex,
    DateTime StartedAt,
    DateTime? CompletedAt,
    List<WorkflowStepExecutionDto> StepExecutions
);
