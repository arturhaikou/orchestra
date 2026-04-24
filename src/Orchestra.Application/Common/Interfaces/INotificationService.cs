using Orchestra.Application.Agents.DTOs;

namespace Orchestra.Application.Common.Interfaces;

public interface INotificationService
{
    Task NotifyAgentExecutionCompletedAsync(
        AgentExecutionCompletedNotification notification,
        CancellationToken cancellationToken = default);
}
