using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Infrastructure.Hubs;

namespace Orchestra.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyAgentExecutionCompletedAsync(
        AgentExecutionCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"workspace-{notification.WorkspaceId}";

        var payload = new
        {
            agentId = notification.AgentId,
            agentName = notification.AgentName,
            ticketId = notification.TicketId,
            ticketTitle = notification.TicketTitle,
            status = notification.Status,
            summary = notification.Summary,
            reviewUrl = notification.ReviewUrl
        };

        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("AgentExecutionCompleted", payload, cancellationToken);

            _logger.LogDebug(
                "Dispatched AgentExecutionCompleted notification. WorkspaceId={WorkspaceId} AgentId={AgentId} TicketId={TicketId} Status={Status}",
                notification.WorkspaceId,
                notification.AgentId,
                notification.TicketId,
                notification.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch AgentExecutionCompleted notification. WorkspaceId={WorkspaceId} AgentId={AgentId} TicketId={TicketId}",
                notification.WorkspaceId,
                notification.AgentId,
                notification.TicketId);
        }
    }
}
