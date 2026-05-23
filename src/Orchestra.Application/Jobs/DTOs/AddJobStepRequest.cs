using Orchestra.Domain.Enums;

namespace Orchestra.Application.Jobs.DTOs;

public record AddJobStepRequest(
    JobStepType StepType,
    string? Content = null,
    string? ToolName = null,
    bool IsJson = false,
    long? DurationMs = null,
    bool IsError = false);
