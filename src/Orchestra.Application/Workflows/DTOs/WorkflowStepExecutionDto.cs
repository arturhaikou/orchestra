using Orchestra.Domain.Enums;

namespace Orchestra.Application.Workflows.DTOs;

public record WorkflowStepExecutionDto(
    Guid Id,
    Guid WorkflowExecutionId,
    int StepIndex,
    Guid? JobId,
    WorkflowExecutionStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? Output
);
