using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.DTOs;

public record TemplateDefinition(
    string TemplateId,
    string Name,
    string Role,
    string Description,
    IReadOnlyList<IntegrationType> RequiredIntegrationTypes,
    IReadOnlyList<string> ToolMethodNames,
    IReadOnlyList<string> LockedFields,
    IReadOnlyList<string> DefaultCapabilities,
    int TemplateVersion
);
