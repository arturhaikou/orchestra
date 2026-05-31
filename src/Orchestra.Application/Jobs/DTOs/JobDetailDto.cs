using Orchestra.Domain.Enums;

namespace Orchestra.Application.Jobs.DTOs;

public record JobDetailDto(
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
    string InitialPrompt,
    string? FinalResponse,
    string? ErrorMessage,
    IReadOnlyList<JobStepDto> Steps,
    Guid? ParentJobId = null,
    Guid? WorkflowExecutionId = null);
