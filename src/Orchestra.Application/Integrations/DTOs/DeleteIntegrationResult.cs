namespace Orchestra.Application.Integrations.DTOs;

public record DeleteIntegrationResult(
    int DeactivatedToolActions,
    int DeletedAgentToolActionAssignments,
    int DeactivatedToolCategories
);
