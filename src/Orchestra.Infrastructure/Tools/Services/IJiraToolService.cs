using System.ComponentModel;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;
using Orchestra.Infrastructure.Tools.Models.Jira;
using System.Collections.Generic;

namespace Orchestra.Infrastructure.Tools.Services;

[ToolCategory("Jira", ProviderType.JIRA, "Manage Jira issues and epics")]
public interface IJiraToolService
{
[ToolAction("create_issue", "Create a new Jira issue", DangerLevel.Moderate)]
    [Description("Create a new Jira issue with summary, description, and issue type")]
    Task<object> CreateIssueAsync(
        [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
        [Description("Brief summary of the issue")] string summary,
        [Description("Detailed description of the issue in markdown format")] string description,
        [Description("The issue type name (e.g., Bug, Story, Task, Epic)")] string issueTypeName);

    [ToolAction("update_issue", "Update an existing Jira issue", DangerLevel.Moderate)]
    [Description("Update an existing Jira issue's summary and/or description")]
    Task<object> UpdateIssueAsync(
        [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
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
        [Description("The Jira issue key to retrieve (e.g., PROJ-123)")] string issueKey);

    [ToolAction("create_epic", "Create a new Jira epic", DangerLevel.Moderate)]
    [Description("Create a new Jira epic with multiple child stories")]
    Task<object> CreateEpicAsync(
        [Description("The workspace ID where the Jira integration is configured")] string workspaceId,
        [Description("The title of the epic")] string epicTitle,
        [Description("The description of the epic in markdown format")] string epicDescription,
        [Description("List of user stories to create under the epic")] List<StoryRequest> stories,
        [Description("The issue type name for stories (default: Story)")] string storyTypeName = "Story");
}