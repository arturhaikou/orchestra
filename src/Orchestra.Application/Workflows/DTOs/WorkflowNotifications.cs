using Orchestra.Domain.Enums;

namespace Orchestra.Application.Workflows.DTOs;

public record WorkflowStepStartedNotification(
    Guid WorkspaceId,
    Guid WorkflowExecutionId,
    Guid TicketId,
    int StepIndex
);

public record WorkflowStepCompletedNotification(
    Guid WorkspaceId,
    Guid WorkflowExecutionId,
    Guid TicketId,
    int StepIndex,
    WorkflowExecutionStatus Status
);

public record WorkflowExecutionStatusChangedNotification(
    Guid WorkspaceId,
    Guid WorkflowExecutionId,
    Guid TicketId,
    WorkflowExecutionStatus Status
);
