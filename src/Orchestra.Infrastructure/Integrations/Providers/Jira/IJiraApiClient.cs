using System.Net.Http;
using System.Text.Json;
using Orchestra.Infrastructure.Integrations.Providers.Jira.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

/// <summary>
/// Abstract API client interface for Jira operations.
/// Implementations support both Cloud (v3) and On-Premise (v2) API versions.
/// </summary>
public interface IJiraApiClient
{
    /// <summary>
    /// Searches for issues using JQL (Jira Query Language).
    /// </summary>
    /// <param name="jql">The JQL query string.</param>
    /// <param name="fields">Comma-separated list of fields to retrieve.</param>
    /// <param name="startAt">Pagination offset.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search response containing issues and pagination info.</returns>
    Task<JiraSearchResponse> SearchIssuesAsync(
        string jql,
        string fields,
        int startAt = 0,
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single issue by key.
    /// </summary>
    /// <param name="issueKey">The issue key (e.g., PROJECT-123).</param>
    /// <param name="fields">Comma-separated list of fields to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Jira ticket, or null if not found.</returns>
    Task<JiraTicket?> GetIssueAsync(
        string issueKey,
        string fields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new issue.
    /// </summary>
    /// <param name="request">The create issue request with fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The create issue response with issue key and ID.</returns>
    Task<CreateIssueResponse> CreateIssueAsync(
        CreateIssueRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing issue.
    /// </summary>
    /// <param name="issueKey">The issue key to update.</param>
    /// <param name="fields">The fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateIssueAsync(
        string issueKey,
        object fields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an issue.
    /// </summary>
    /// <param name="issueKey">The issue key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteIssueAsync(
        string issueKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to an issue.
    /// </summary>
    /// <param name="issueKey">The issue key.</param>
    /// <param name="body">The comment body (format depends on API version).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddCommentAsync(
        string issueKey,
        object body,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available issue types.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available issue types.</returns>
    Task<List<IssueType>> GetIssueTypesAsync(
        CancellationToken cancellationToken = default);
}
