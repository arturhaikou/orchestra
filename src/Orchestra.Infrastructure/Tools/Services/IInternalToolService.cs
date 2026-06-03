using System.Collections.Generic;
using System.ComponentModel;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;
using Orchestra.Infrastructure.Tools.Models;

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

    [ToolAction("add_comment", "Add a text comment to an internal ticket", DangerLevel.Moderate)]
    [Description("Add a plain markdown comment to an internal ticket")]
    Task<object> AddCommentAsync(
        [Description("The workspace ID where the ticket exists")] string workspaceId,
        [Description("The ticket ID (GUID)")] string ticketId,
        [Description("The comment text in markdown format")] string comment);

    [ToolAction("add_comment_with_images", "Add a rich comment with inline images to an internal ticket", DangerLevel.Moderate)]
    [Description("Add a comment with interleaved text paragraphs and images to an internal ticket. " +
                 "Image paths must be absolute (e.g. C:\\\\Users\\\\...\\\\image.png) — relative paths are rejected.")]
    Task<object> AddCommentWithImagesAsync(
        [Description("The workspace ID where the ticket exists")] string workspaceId,
        [Description("The ticket ID (GUID)")] string ticketId,
        [Description("Ordered list of content blocks. Each block: " +
                     "{\"type\":\"text\",\"content\":\"markdown text\"} OR " +
                     "{\"type\":\"image\",\"content\":\"absolute local file path (e.g. C:\\\\Users\\\\...\\\\image.png — must be absolute, not relative)\",\"fileName\":\"optional display name\"}")]
        List<ContentBlock> contentBlocks);
}