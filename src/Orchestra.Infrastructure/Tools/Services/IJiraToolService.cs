using System.ComponentModel;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;
using Orchestra.Infrastructure.Tools.Models;
using Orchestra.Infrastructure.Tools.Models.Jira;
using System.Collections.Generic;

namespace Orchestra.Infrastructure.Tools.Services;

[ToolCategory("Jira", ProviderType.JIRA, "Manage Jira issues and epics")]
public interface IJiraToolService
{
    [ToolAction("create_issue", "Create a new Jira issue", DangerLevel.Moderate)]
    [Description("Create a new Jira issue with summary, description blocks, and issue type")]
    Task<object> CreateIssueAsync(
            [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
            [Description("The ID of the specific Jira integration instance to use. Required when the workspace has multiple Jira integrations configured.")] string integrationId,
            [Description("Brief summary of the issue")] string summary,
            [Description("The issue type name (e.g., Bug, Story, Task, Epic)")] string issueTypeName,
            [Description("Ordered list of content blocks for the issue description. " +
                         "Each block: {\"type\":\"text\",\"content\":\"markdown text\"} OR " +
                         "{\"type\":\"image\",\"content\":\"https://url OR absolute local file path (e.g. C:\\\\Users\\\\...\\\\image.png — must be absolute, not relative)\",\"fileName\":\"optional\"}")]
            List<ContentBlock> descriptionBlocks);

    [ToolAction("update_issue", "Update an existing Jira issue", DangerLevel.Moderate)]
    [Description("Update an existing Jira issue's summary and/or description")]
    Task<object> UpdateIssueAsync(
        [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
        [Description("The ID of the specific Jira integration instance to use. Required when the workspace has multiple Jira integrations configured.")] string integrationId,
        [Description("The Jira issue key (e.g., PROJ-123)")] string issueKey,
        [Description("Optional new summary for the issue")] string? summary = null,
        [Description("Optional new description in markdown format")] string? description = null);

    // [ToolAction("delete_issue", "Delete a Jira issue", DangerLevel.Destructive)]
    // [Description("Permanently delete a Jira issue by key")]
    // Task<object> DeleteIssueAsync(
    //     [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
    //     [Description("The Jira issue key to delete (e.g., PROJ-123)")] string issueKey);

    [ToolAction("get_issue", "Get a Jira issue by key", DangerLevel.Safe)]
    [Description("Retrieve detailed information about a Jira issue by its key")]
    Task<object> GetIssueAsync(
        [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
        [Description("The ID of the specific Jira integration instance to use. Required when the workspace has multiple Jira integrations configured.")] string integrationId,
        [Description("The Jira issue key to retrieve (e.g., PROJ-123)")] string issueKey);

    [ToolAction("add_comment", "Add a comment to a Jira issue", DangerLevel.Moderate)]
    [Description("Add a comment to an existing Jira issue. Supports interleaved text and images via content blocks.")]
    Task<object> AddCommentAsync(
        [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
        [Description("The ID of the specific Jira integration instance to use. Required when the workspace has multiple Jira integrations configured.")] string integrationId,
        [Description("The Jira issue key to comment on (e.g., PROJ-123)")] string issueKey,
        [Description("Ordered list of content blocks. Each block: " +
                     "{\"type\":\"text\",\"content\":\"markdown text\"} OR " +
                     "{\"type\":\"image\",\"content\":\"https://url OR absolute local file path (e.g. C:\\\\Users\\\\...\\\\image.png — must be absolute, not relative)\",\"fileName\":\"optional display name\"}")]
        List<ContentBlock> contentBlocks);

    [ToolAction("create_epic", "Create a new Jira epic", DangerLevel.Moderate)]
    [Description("Create a new Jira epic with multiple child stories")]
    Task<object> CreateEpicAsync(
        [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
        [Description("The ID of the specific Jira integration instance to use. Required when the workspace has multiple Jira integrations configured.")] string integrationId,
        [Description("The title of the epic")] string epicTitle,
        [Description("The description of the epic in markdown format")] string epicDescription,
        [Description("List of user stories to create under the epic")] List<StoryRequest> stories,
        [Description("The issue type name for stories (default: Story)")] string storyTypeName = "Story");
}