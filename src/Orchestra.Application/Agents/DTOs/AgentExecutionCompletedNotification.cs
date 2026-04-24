namespace Orchestra.Application.Agents.DTOs;

public record AgentExecutionCompletedNotification(
    Guid WorkspaceId,
    Guid AgentId,
    string AgentName,
    Guid TicketId,
    string TicketTitle,
    string Status,
    string Summary,
    string? ReviewUrl);
