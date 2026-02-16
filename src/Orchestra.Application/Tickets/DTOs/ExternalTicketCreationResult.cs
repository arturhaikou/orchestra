namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Result of creating an external ticket in a tracker system (Jira, Azure DevOps, etc.).
/// </summary>
/// <param name="IssueKey">The external issue key (e.g., "PROJ-123" for Jira).</param>
/// <param name="IssueUrl">The full URL to view the issue in the external system.</param>
/// <param name="IssueId">The internal ID from the external system (provider-specific).</param>
public record ExternalTicketCreationResult(
    string IssueKey,
    string IssueUrl,
    string IssueId
);
