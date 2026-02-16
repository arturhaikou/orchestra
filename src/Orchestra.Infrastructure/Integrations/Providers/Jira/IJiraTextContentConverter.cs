using System.Text.Json;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

/// <summary>
/// Abstraction for converting between Markdown and Jira-native content formats.
/// Handles version-specific conversions: ADF for Cloud, HTML for On-Premise.
/// </summary>
public interface IJiraTextContentConverter
{
    /// <summary>
    /// Converts markdown to the appropriate comment body format for the Jira instance.
    /// </summary>
    /// <param name="markdown">Markdown content to convert.</param>
    /// <param name="jiraType">The Jira instance type (Cloud or On-Premise).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The comment body object in the appropriate format.</returns>
    Task<object> ConvertMarkdownToCommentBodyAsync(
        string markdown,
        JiraType jiraType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts markdown to the appropriate description format for the Jira instance.
    /// </summary>
    /// <param name="markdown">Markdown content to convert.</param>
    /// <param name="jiraType">The Jira instance type (Cloud or On-Premise).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The description body object in the appropriate format (ADF for Cloud, string for On-Premise).</returns>
    Task<object> ConvertMarkdownToDescriptionAsync(
        string markdown,
        JiraType jiraType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a comment body from its native format to markdown.
    /// </summary>
    /// <param name="body">The comment body in native format (ADF or HTML).</param>
    /// <param name="jiraType">The Jira instance type (Cloud or On-Premise).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The comment text in markdown format.</returns>
    Task<string?> ConvertCommentBodyToMarkdownAsync(
        JsonElement body,
        JiraType jiraType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a description from its native format to markdown.
    /// </summary>
    /// <param name="description">The description in native format (ADF or HTML).</param>
    /// <param name="jiraType">The Jira instance type (Cloud or On-Premise).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The description in markdown format.</returns>
    Task<string?> ConvertDescriptionToMarkdownAsync(
        JsonElement description,
        JiraType jiraType,
        CancellationToken cancellationToken = default);
}
