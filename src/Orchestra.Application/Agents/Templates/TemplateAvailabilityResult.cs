namespace Orchestra.Application.Agents.Templates;

internal record TemplateAvailabilityResult(
    string TemplateId,
    bool IsAvailable,
    string? UnavailabilityReason,
    string? ProviderLabel,
    string ResolvedGuideText,
    bool IsDeployed,
    Guid? DeployedAgentId);
