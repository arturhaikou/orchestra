namespace Orchestra.Application.Integrations.DTOs;

public record DeletionImpactDto(
    int ToolActionsToDeactivate,
    int AgentAssignmentsToRemove,
    bool ToolCategoryWillDeactivate
);
