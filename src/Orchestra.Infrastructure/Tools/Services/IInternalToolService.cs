using System.ComponentModel;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;

namespace Orchestra.Infrastructure.Tools.Services;

[ToolCategory("Internal", ProviderType.INTERNAL, "Manage internal system tickets")]
public interface IInternalToolService
{
    [ToolAction("create_ticket", "Create an internal ticket", DangerLevel.Moderate)]
    [Description("Create a new internal ticket in the system with specified status and priority")]
    Task<object> CreateTicketAsync(
        [Description("The workspace ID where the ticket will be created")] string workspaceId,
        [Description("The title of the ticket")] string title,
        [Description("The description of the ticket")] string description);

    [ToolAction("get_ticket", "Retrieve ticket details", DangerLevel.Safe)]
    [Description("Retrieve detailed information about an internal ticket by its ID")]
    Task<object> GetTicketAsync(
        [Description("The workspace ID where the ticket exists")] string workspaceId,
        [Description("The ticket ID to retrieve")] string ticketId);

    [ToolAction("update_ticket", "Update an internal ticket", DangerLevel.Moderate)]
    [Description("Update status, priority, assigned agent, or workflow for an internal ticket")]
    Task<object> UpdateTicketAsync(
        [Description("The workspace ID where the ticket exists")] string workspaceId,
        [Description("The ticket ID to update")] string ticketId,
        [Description("Optional agent ID to assign the ticket to")] string? assignedAgentId = null,
        [Description("Optional workflow ID to assign the ticket to")] string? assignedWorkflowId = null);
}