namespace Orchestra.Application.Tickets.DTOs;

public record TicketStatusChangedNotification(
    Guid WorkspaceId,
    string TicketId,
    string NewStatus,
    string? PreviousStatus);
