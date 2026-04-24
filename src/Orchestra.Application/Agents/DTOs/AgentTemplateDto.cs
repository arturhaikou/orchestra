namespace Orchestra.Application.Agents.DTOs;

public record AgentTemplateDto(
    string TemplateId,
    string Name,
    string Role,
    string Description,
    IReadOnlyList<TemplatePrerequisiteDto> Prerequisites,
    TemplateAvailabilityDto Availability,
    IReadOnlyList<string> Capabilities,
    string ToolLabel,
    string UsageGuide,
    int TemplateVersion
);

public record TemplatePrerequisiteDto(
    string IntegrationType,
    string ProviderName,
    bool Satisfied
);

public record TemplateAvailabilityDto(
    string Status,
    string? Reason,
    Guid? ExistingAgentId
);
