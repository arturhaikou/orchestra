namespace Orchestra.Application.Workflows.DTOs;

public record WorkflowDefinitionDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Description,
    List<WorkflowStepDto> Steps,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
