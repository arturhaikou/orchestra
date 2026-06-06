namespace Orchestra.Application.Workflows.DTOs;

public record WorkflowStepDto(
    Guid Id,
    Guid WorkflowDefinitionId,
    int Order,
    Guid? AgentId,
    string AgentName,
    string? InstructionOverride,
    bool PassPreviousOutput,
    List<string> SystemTools,
    string StepType = "Agent",
    string? Condition = null,
    Guid? TrueNextStepId = null,
    Guid? FalseNextStepId = null
);
