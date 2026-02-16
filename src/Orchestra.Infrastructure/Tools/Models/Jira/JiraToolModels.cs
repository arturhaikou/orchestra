using System.Text.Json;
using System.Text.Json.Serialization;
using Orchestra.Infrastructure.Integrations.Providers.Jira.Models;

namespace Orchestra.Infrastructure.Tools.Models.Jira;

/// <summary>
/// Represents a story to be created under an epic.
/// Used by CreateEpicAsync method.
/// </summary>
/// <param name="Title">The story title/summary.</param>
/// <param name="Description">The story description content.</param>
public record StoryRequest(
    string Title,
    string Description);

/// <summary>
/// Request body for JIRA issue update API (PUT /rest/api/3/issue/{issueKey}).
/// Used by UpdateIssueAsync method.
/// </summary>
public class UpdateIssueRequest
{
    /// <summary>
    /// Fields to update in the JIRA issue.
    /// </summary>
    public UpdateIssueFields Fields { get; set; } = new();
}

/// <summary>
/// Fields that can be updated in a JIRA issue.
/// Only non-null fields will be included in the API request.
/// </summary>
public class UpdateIssueFields
{
    /// <summary>
    /// The issue summary/title.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// The issue description in Atlassian Document Format (ADF).
    /// </summary>
    [JsonPropertyName("description")]
    public JsonElement? Description { get; set; }
}