namespace Orchestra.Application.Agents.DTOs;

public record CreateAgentFromTemplateRequest(
    Guid WorkspaceId,
    string TemplateId,
    string ProjectPrinciples,
    string? Model);
