namespace Orchestra.Application.Workflows.DTOs;

public record CreateWorkflowDefinitionRequest(
    Guid WorkspaceId,
    string Name,
    string? Description,
    List<CreateWorkflowStepRequest> Steps
);

public record CreateWorkflowStepRequest(
    int Order,
    Guid AgentId,
    string? InstructionOverride,
    bool PassPreviousOutput
);

public record UpdateWorkflowDefinitionRequest(
    string Name,
    string? Description,
    List<CreateWorkflowStepRequest> Steps
);
