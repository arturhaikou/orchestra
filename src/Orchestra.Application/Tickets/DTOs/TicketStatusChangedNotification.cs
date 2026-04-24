namespace Orchestra.Application.Tickets.DTOs;

public record TicketStatusChangedNotification(
    Guid WorkspaceId,
    Guid TicketId,
    string NewStatus,
    string? PreviousStatus);
