using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Tickets.DTOs;

namespace Orchestra.Application.Common.Interfaces;

public interface INotificationService
{
    Task NotifyAgentExecutionCompletedAsync(
        AgentExecutionCompletedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyTicketStatusChangedAsync(
        TicketStatusChangedNotification notification,
        CancellationToken cancellationToken = default);
}
