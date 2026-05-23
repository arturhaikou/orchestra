using Orchestra.Domain.Enums;

namespace Orchestra.Application.Jobs.DTOs;

public record JobStepDto(
    Guid Id,
    JobStepType StepType,
    int Sequence,
    DateTime Timestamp,
    string? Content,
    string? ToolName,
    bool IsJson,
    long? DurationMs,
    bool IsError,
    Guid? ParentStepId = null,
    Guid? AgentId = null,
    string? AgentName = null);
