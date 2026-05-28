namespace Orchestra.Application.Agents.DTOs;

public record ResolvedTemplate(
    string TemplateId,
    TemplateAvailabilityStatus Status,
    string? UnavailabilityReason,
    Guid? ExistingAgentId,
    List<ResolvedToolAction> ResolvedToolActions,
    List<ProviderLabel> ProviderLabels,
    string? ResolvedGuide,
    List<OptionalToolDto> AvailableOptionalTools
);
