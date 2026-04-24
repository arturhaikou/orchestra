namespace Orchestra.Application.Agents.DTOs;

public record TemplateAvailability(
    string TemplateId,
    TemplateAvailabilityStatus Status,
    string? Reason,
    Guid? ExistingAgentId
);
