using Orchestra.Domain.Enums;

namespace Orchestra.Application.Jobs.DTOs;

public record CreateJobRequest(
    Guid WorkspaceId,
    Guid AgentId,
    string AgentName,
    JobTriggerType TriggerType,
    string InitialPrompt,
    Guid? TicketId = null,
    string? TicketTitle = null);
