namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Flat projection DTO produced by the paginated GetTicketsByWorkspaceAsync query.
/// Combines scalar fields from the <c>Tickets</c> table with scalar fields from the
/// <c>Integrations</c> table resolved via a LEFT JOIN.
/// No navigation properties; all related data is projected inline at query time.
/// </summary>
public class TicketWithIntegrationDto
{
    // ----- Core Ticket fields -----
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? PriorityId { get; set; }
    public Guid? StatusId { get; set; }
    public bool IsInternal { get; set; }
    public Guid? IntegrationId { get; set; }
    public string? ExternalTicketId { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public Guid? AssignedWorkflowId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ----- Integration fields (null when IntegrationId is null / no LEFT JOIN match) -----
    public string? IntegrationName { get; set; }
    public string? IntegrationUrl { get; set; }

    /// <summary>
    /// <c>ProviderType</c> enum value serialised to a string (e.g. "JIRA", "AZURE_DEVOPS").
    /// Null when the ticket has no integration (pure internal ticket).
    /// Used to populate <c>TicketDto.Source</c>.
    /// </summary>
    public string? IntegrationProvider { get; set; }
}
