namespace Orchestra.Application.Workflows.DTOs;

public record CreateWorkflowDefinitionRequest(
    Guid WorkspaceId,
    string Name,
    string? Description,
    List<CreateWorkflowStepRequest> Steps
);

public record CreateWorkflowStepRequest(
    int Order,
    Guid? AgentId,
    string? InstructionOverride,
    bool PassPreviousOutput,
    List<string>? SystemTools = null,
    string? ClientId = null,
    string? Type = null,
    string? Condition = null,
    string? TrueNextClientId = null,
    string? FalseNextClientId = null
);

public record UpdateWorkflowDefinitionRequest(
    string Name,
    string? Description,
    List<CreateWorkflowStepRequest> Steps
);
