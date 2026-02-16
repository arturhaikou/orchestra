using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Abstraction for external ticket provider operations (Jira, Azure DevOps, etc.).
/// Implementations fetch tickets from external systems via their respective APIs.
/// </summary>
public interface ITicketProvider
{
    /// <summary>
    /// Fetches tickets from the external provider with pagination support.
    /// </summary>
    /// <param name="integration">Integration configuration with credentials and filter query.</param>
    /// <param name="startAt">Pagination start index (0-based).</param>
    /// <param name="maxResults">Maximum number of results per page.</param>
    /// <param name="pageToken">Continuation token from previous request (provider-specific format).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// Tuple containing:
    /// - Tickets: List of external tickets mapped to ExternalTicketDto
    /// - IsLast: True if this is the last page of results
    /// - NextPageToken: Token for fetching next page (null if IsLast = true)
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when provider API call fails.</exception>
    Task<(List<ExternalTicketDto> Tickets, bool IsLast, string? NextPageToken)> FetchTicketsAsync(
        Integration integration,
        int startAt = 0,
        int maxResults = 50,
        string? pageToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single ticket by its external ID.
    /// </summary>
    /// <param name="integration">Integration configuration with credentials.</param>
    /// <param name="externalTicketId">External ticket identifier (e.g., "PROJ-123" for Jira).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// ExternalTicketDto if found, null if ticket does not exist in external system.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when provider API call fails.</exception>
    Task<ExternalTicketDto?> GetTicketByIdAsync(
        Integration integration,
        string externalTicketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to an external ticket via provider API.
    /// </summary>
    /// <param name="integration">Integration configuration with credentials.</param>
    /// <param name="externalTicketId">External ticket identifier.</param>
    /// <param name="content">Comment text content.</param>
    /// <param name="author">Comment author name (retrieved from database).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The created comment with provider-assigned ID and timestamp.</returns>
    /// <exception cref="HttpRequestException">Thrown when provider API call fails.</exception>
    Task<CommentDto> AddCommentAsync(
        Integration integration,
        string externalTicketId,
        string content,
        string author,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new issue in the external tracker system.
    /// </summary>
    /// <param name="integration">Integration configuration with credentials and project filter.</param>
    /// <param name="summary">The issue title/summary.</param>
    /// <param name="description">The issue description in Markdown format.</param>
    /// <param name="issueTypeName">The issue type name (e.g., "Task", "Story", "Bug", "Epic").</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing the created issue key, URL, and ID.</returns>
    /// <exception cref="HttpRequestException">Thrown when provider API call fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when issue type or project cannot be resolved.</exception>
    Task<ExternalTicketCreationResult> CreateIssueAsync(
        Integration integration,
        string summary,
        string description,
        string issueTypeName,
        CancellationToken cancellationToken = default);
}