using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Workflows.DTOs;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Tools.WorkflowTools;

public static class SwitchWorkflowTicketFunction
{
    public static AIFunction Create(
        JobTrackingContext jobTracking,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        return AIFunctionFactory.Create(
            async (
                [Description("The external ticket key to switch to (e.g. 'PROJ-123', 'GH-42').")]
                string externalTicketKey,
                [Description("The integration ID (GUID string) of the external provider where the ticket was created.")]
                string integrationId,
                CancellationToken cancellationToken) =>
            {
                if (!jobTracking.WorkflowExecutionId.HasValue)
                    return "Error: this tool can only be used within a workflow execution.";

                if (!Guid.TryParse(integrationId, out var integrationGuid))
                    return $"Error: '{integrationId}' is not a valid integration ID.";

                await using var scope = scopeFactory.CreateAsyncScope();
                var executionRepository = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
                var ticketDataAccess = scope.ServiceProvider.GetRequiredService<ITicketDataAccess>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var ticketIdParsingService = scope.ServiceProvider.GetRequiredService<ITicketIdParsingService>();

                var execution = await executionRepository.GetByIdAsync(
                    jobTracking.WorkflowExecutionId.Value, cancellationToken);
                if (execution is null)
                    return "Error: workflow execution not found.";

                var previousTicketId = execution.ActiveTicketId ?? execution.TicketId;

                var ticket = await ticketDataAccess.GetTicketByExternalIdAsync(
                    integrationGuid, externalTicketKey, cancellationToken);

                if (ticket is null)
                {
                    ticket = Ticket.MaterializeFromExternal(
                        workspaceId: execution.WorkspaceId,
                        integrationId: integrationGuid,
                        externalTicketId: externalTicketKey,
                        title: externalTicketKey,
                        description: string.Empty);

                    await ticketDataAccess.AddTicketAsync(ticket, cancellationToken);
                }

                execution.SwitchActiveTicket(ticket.Id);
                await executionRepository.UpdateAsync(execution, cancellationToken);

                // Transition old ticket → Completed
                var previousTicket = await ticketDataAccess.GetTicketByIdAsync(previousTicketId, cancellationToken);
                if (previousTicket is not null)
                {
                    previousTicket.UpdateStatus(Guid.Parse("88888888-8888-8888-8888-888888888888"));
                    await ticketDataAccess.UpdateTicketAsync(previousTicket, cancellationToken);
                    var prevCompositeId = BuildNotificationTicketId(previousTicket, ticketIdParsingService);
                    await notificationService.NotifyTicketStatusChangedAsync(
                        new TicketStatusChangedNotification(execution.WorkspaceId, prevCompositeId, "Completed", "In Progress"),
                        cancellationToken);
                }

                // Assign workflow + transition new ticket → In Progress
                ticket.UpdateAssignments(ticket.AssignedAgentId, null, execution.WorkflowDefinitionId, execution.WorkspaceId);
                ticket.UpdateStatus(Guid.Parse("77777777-7777-7777-7777-777777777777"));
                await ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);
                var newCompositeId = BuildNotificationTicketId(ticket, ticketIdParsingService);
                await notificationService.NotifyTicketStatusChangedAsync(
                    new TicketStatusChangedNotification(execution.WorkspaceId, newCompositeId, "In Progress", "To Do"),
                    cancellationToken);

                await notificationService.NotifyWorkflowTicketSwitchedAsync(
                    new WorkflowTicketSwitchedNotification(
                        WorkspaceId: execution.WorkspaceId,
                        WorkflowExecutionId: execution.Id,
                        WorkflowId: execution.WorkflowDefinitionId,
                        PreviousTicketId: previousTicketId,
                        NewTicketId: ticket.Id,
                        ExternalTicketKey: externalTicketKey),
                    cancellationToken);

                logger.LogInformation(
                    "Workflow execution {ExecutionId} switched active ticket to {ExternalTicketKey} (ticket {TicketId})",
                    execution.Id, externalTicketKey, ticket.Id);

                return $"Workflow context switched to {externalTicketKey} (ticket ID: {ticket.Id}). All subsequent steps will work on this ticket.";
            },
            name: "switch_workflow_ticket",
            description:
                "Redirects the rest of this workflow to a different ticket. " +
                "Call this immediately after creating an external ticket (Jira, GitHub, GitLab) " +
                "to make all subsequent workflow steps work on the newly created ticket instead of the original one. " +
                "Pass the external issue key (e.g. 'PROJ-123') and the integration ID.");
    }

    private static string BuildNotificationTicketId(Ticket ticket, ITicketIdParsingService svc) =>
        ticket.IntegrationId.HasValue && !string.IsNullOrEmpty(ticket.ExternalTicketId)
            ? svc.BuildCompositeId(ticket.IntegrationId.Value, ticket.ExternalTicketId)
            : ticket.Id.ToString();
}
