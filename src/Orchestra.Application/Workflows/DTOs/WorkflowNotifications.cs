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

public record WorkflowStepJobAssignedNotification(
    Guid WorkspaceId,
    Guid WorkflowExecutionId,
    Guid TicketId,
    int StepIndex,
    Guid JobId
);

public record WorkflowTicketSwitchedNotification(
    Guid WorkspaceId,
    Guid WorkflowExecutionId,
    Guid PreviousTicketId,
    Guid NewTicketId,
    string ExternalTicketKey
);
