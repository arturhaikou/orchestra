using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Workflows.DTOs;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Tools.WorkflowTools;

public static class SwitchWorkflowTicketFunction
{
    public static AIFunction Create(
        JobTrackingContext jobTracking,
        IWorkflowExecutionRepository executionRepository,
        ITicketDataAccess ticketDataAccess,
        INotificationService notificationService,
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

                await notificationService.NotifyWorkflowTicketSwitchedAsync(
                    new WorkflowTicketSwitchedNotification(
                        WorkspaceId: execution.WorkspaceId,
                        WorkflowExecutionId: execution.Id,
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
}
